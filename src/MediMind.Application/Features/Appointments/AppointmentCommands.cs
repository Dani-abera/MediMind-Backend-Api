using FluentValidation;
using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.Appointments;

// ─── Shared DTOs ──────────────────────────────────────────────────────────────

public record AppointmentDto(
    Guid Id,
    Guid PatientId,
    string PatientName,
    Guid DoctorId,
    string DoctorName,
    string Specialization,
    Guid CenterId,
    string CenterName,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    int DurationMinutes,
    string Status,
    string ReasonForVisit,
    string? Symptoms,
    DateTime BookingDate,
    string? Notes);

// ═══════════════════════════════════════════════════════════════════════════════
// BOOK APPOINTMENT
// ═══════════════════════════════════════════════════════════════════════════════

public record BookAppointmentCommand(
    Guid PatientId,
    Guid DoctorId,
    Guid CenterId,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    string ReasonForVisit,
    string? Symptoms
) : IRequest<BookAppointmentResult>;

public record BookAppointmentResult(
    Guid AppointmentId,
    string QueueNumber,
    string Message);

public class BookAppointmentValidator : AbstractValidator<BookAppointmentCommand>
{
    public BookAppointmentValidator()
    {
        RuleFor(x => x.PatientId).NotEmpty();
        RuleFor(x => x.DoctorId).NotEmpty();
        RuleFor(x => x.CenterId).NotEmpty();
        RuleFor(x => x.AppointmentDate)
            .Must(d => d >= DateOnly.FromDateTime(DateTime.Today))
            .WithMessage("Appointment date cannot be in the past.");
        RuleFor(x => x.ReasonForVisit)
            .NotEmpty().WithMessage("Reason for visit is required.")
            .MaximumLength(500);
    }
}

public class BookAppointmentHandler(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository centerRepository,
    IDoctorRepository doctorRepository,
    IQueueHubService queueHub,
    ISmsService smsService,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<BookAppointmentCommand, BookAppointmentResult>
{
    public async Task<BookAppointmentResult> Handle(BookAppointmentCommand request, CancellationToken ct)
    {
        var center = await centerRepository.GetByIdAsync(request.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), request.CenterId);

        if (!center.IsSubscriptionActive)
            throw new DomainException("This healthcare center's subscription is not active.");

        // Rule: max 1 appointment per doctor per day per patient
        if (await appointmentRepository.PatientHasAppointmentTodayAsync(
                request.PatientId, request.DoctorId, request.AppointmentDate, ct))
            throw new DomainException($"You already have an appointment with this doctor on {request.AppointmentDate}.");

        // Rule: check slot availability (unique constraint also enforces this at DB level)
        if (!await appointmentRepository.IsSlotAvailableAsync(
                request.DoctorId, request.CenterId, request.AppointmentDate, request.AppointmentTime, ct))
            throw new DomainException("This time slot is no longer available. Please choose another time.");

        var appointment = Appointment.Book(
            request.PatientId,
            request.DoctorId,
            request.CenterId,
            request.AppointmentDate,
            request.AppointmentTime,
            center.SlotDurationMinutes,
            request.ReasonForVisit,
            request.Symptoms,
            center.AdvanceBookingDays,
            minimumHoursNotice: 2);

        await appointmentRepository.AddAsync(appointment, ct);

        // Auto-approve if center has that setting enabled
        if (center.AutoApproveAppointments)
        {
            appointment.Approve(Guid.Empty); // System approval
        }

        await unitOfWork.SaveChangesAsync(ct);

        // Notify patient (push + SMS)
        await pushService.SendToUserAsync(
            request.PatientId,
            "Appointment Booked",
            $"Your appointment on {request.AppointmentDate} at {request.AppointmentTime} has been booked.",
            new Dictionary<string, string> { ["appointmentId"] = appointment.Id.ToString() },
            ct);

        var status = center.AutoApproveAppointments ? "confirmed" : "pending admin approval";
        return new BookAppointmentResult(
            appointment.Id,
            string.Empty,  // Queue number assigned at 6 AM
            $"Appointment booked successfully and is {status}.");
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// APPROVE APPOINTMENT (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

public record ApproveAppointmentCommand(Guid AppointmentId, Guid AdminId, Guid CenterId) : IRequest<Unit>;

public class ApproveAppointmentHandler(
    IAppointmentRepository appointmentRepository,
    ISmsService smsService,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<ApproveAppointmentCommand, Unit>
{
    public async Task<Unit> Handle(ApproveAppointmentCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        // Multi-tenant isolation: admin can only approve appointments at their center
        if (appointment.CenterId != request.CenterId)
            throw new TenantIsolationException();

        appointment.Approve(request.AdminId);
        await unitOfWork.SaveChangesAsync(ct);

        // Notify patient
        await pushService.SendToUserAsync(
            appointment.PatientId,
            "Appointment Confirmed ✓",
            $"Your appointment on {appointment.AppointmentDate} at {appointment.AppointmentTime} is confirmed.",
            new Dictionary<string, string> { ["appointmentId"] = appointment.Id.ToString() },
            ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// REJECT APPOINTMENT (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

public record RejectAppointmentCommand(Guid AppointmentId, Guid AdminId, Guid CenterId, string Reason) : IRequest<Unit>;

public class RejectAppointmentHandler(
    IAppointmentRepository appointmentRepository,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<RejectAppointmentCommand, Unit>
{
    public async Task<Unit> Handle(RejectAppointmentCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        if (appointment.CenterId != request.CenterId)
            throw new TenantIsolationException();

        appointment.Reject(request.AdminId, request.Reason);
        await unitOfWork.SaveChangesAsync(ct);

        await pushService.SendToUserAsync(
            appointment.PatientId,
            "Appointment Update",
            $"Your appointment request was not approved. Reason: {request.Reason}",
            null, ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CANCEL APPOINTMENT (Patient or Admin)
// ═══════════════════════════════════════════════════════════════════════════════

public record CancelAppointmentCommand(
    Guid AppointmentId,
    Guid CancelledBy,
    string Reason,
    Guid? CenterId = null   // Required if admin is cancelling
) : IRequest<Unit>;

public class CancelAppointmentHandler(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository centerRepository,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CancelAppointmentCommand, Unit>
{
    public async Task<Unit> Handle(CancelAppointmentCommand request, CancellationToken ct)
    {
        var appointment = await appointmentRepository.GetByIdAsync(request.AppointmentId, ct)
                          ?? throw new NotFoundException(nameof(Appointment), request.AppointmentId);

        var center = await centerRepository.GetByIdAsync(appointment.CenterId, ct)
                     ?? throw new NotFoundException(nameof(HealthcareCenter), appointment.CenterId);

        appointment.Cancel(request.CancelledBy, request.Reason, center.CancellationHours);
        await unitOfWork.SaveChangesAsync(ct);

        await pushService.SendToUserAsync(
            appointment.PatientId,
            "Appointment Cancelled",
            $"Your appointment on {appointment.AppointmentDate} has been cancelled.",
            null, ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET APPOINTMENT QUERIES
// ═══════════════════════════════════════════════════════════════════════════════

public record GetPatientAppointmentsQuery(Guid PatientId) : IRequest<IReadOnlyList<AppointmentDto>>;

public class GetPatientAppointmentsHandler(IAppointmentRepository appointmentRepository)
    : IRequestHandler<GetPatientAppointmentsQuery, IReadOnlyList<AppointmentDto>>
{
    public async Task<IReadOnlyList<AppointmentDto>> Handle(
        GetPatientAppointmentsQuery request, CancellationToken ct)
    {
        var appointments = await appointmentRepository.GetByPatientAsync(request.PatientId, ct);

        return appointments.Select(a => new AppointmentDto(
            a.Id,
            a.PatientId,
            a.Patient?.FullName ?? string.Empty,
            a.DoctorId,
            a.Doctor?.FullName ?? string.Empty,
            a.Doctor?.Specialization ?? string.Empty,
            a.CenterId,
            a.Center?.CenterName ?? string.Empty,
            a.AppointmentDate,
            a.AppointmentTime,
            a.DurationMinutes,
            a.Status.ToString(),
            a.ReasonForVisit,
            a.Symptoms,
            a.BookingDate,
            a.Notes
        )).ToList();
    }
}

public record GetDoctorAvailableSlotsQuery(
    Guid DoctorId,
    Guid CenterId,
    DateOnly Date
) : IRequest<IReadOnlyList<TimeOnly>>;

public class GetDoctorAvailableSlotsHandler(
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository)
    : IRequestHandler<GetDoctorAvailableSlotsQuery, IReadOnlyList<TimeOnly>>
{
    public async Task<IReadOnlyList<TimeOnly>> Handle(
        GetDoctorAvailableSlotsQuery request, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByIdAsync(request.DoctorId, ct)
                     ?? throw new NotFoundException(nameof(Doctor), request.DoctorId);

        var schedule = doctor.Schedules.FirstOrDefault(s => s.CenterId == request.CenterId);
        if (schedule is null || !schedule.IsWorkingDay(request.Date))
            return [];

        var existingAppointments = await appointmentRepository
            .GetByDoctorAndDateAsync(request.DoctorId, request.Date, ct);

        var bookedTimes = existingAppointments
            .Where(a => a.Status is not AppointmentStatus.Cancelled and not AppointmentStatus.NoShow)
            .Select(a => a.AppointmentTime)
            .ToList();

        return schedule.GetAvailableSlots(request.Date, bookedTimes);
    }
}
