using FluentValidation;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Features.Appointments;

/// <summary>
/// Appointment availability computation service.
/// </summary>
public interface IAppointmentAvailabilityService
{
    /// <summary>
    /// Returns all generated slots and availability state for a day.
    /// </summary>
    Task<List<TimeSlot>> GetAvailableSlotsAsync(Guid doctorId, Guid centerId, DateOnly date);

    /// <summary>
    /// Returns dates that have at least one available slot.
    /// </summary>
    Task<List<DateOnly>> GetAvailableDatesAsync(Guid doctorId, Guid centerId, int daysAhead = 30);
}

/// <summary>
/// Booking validation service.
/// </summary>
public interface IBookingValidationService
{
    /// <summary>
    /// Validates booking request against business constraints.
    /// </summary>
    Task ValidateBookingAsync(CreateAppointmentDto dto, Guid patientId);
}

/// <summary>
/// Appointment application service.
/// </summary>
public interface IAppointmentService
{
    /// <summary>
    /// Books a new appointment.
    /// </summary>
    Task<AppointmentResponseDto> BookAppointmentAsync(CreateAppointmentDto dto, Guid patientId);

    /// <summary>
    /// Cancels an appointment.
    /// </summary>
    Task CancelAppointmentAsync(Guid appointmentId, Guid requesterId, CancelAppointmentDto dto, string requesterRole, Guid? centerId = null);

    /// <summary>
    /// Reschedules a patient appointment.
    /// </summary>
    Task<AppointmentResponseDto> RescheduleAppointmentAsync(Guid appointmentId, Guid patientId, RescheduleAppointmentDto dto);

    /// <summary>
    /// Approves a pending appointment.
    /// </summary>
    Task<AppointmentResponseDto> ApproveAppointmentAsync(Guid appointmentId, Guid adminId, Guid adminCenterId);

    /// <summary>
    /// Rejects a pending appointment.
    /// </summary>
    Task RejectAppointmentAsync(Guid appointmentId, Guid adminId, Guid adminCenterId, string reason);

    /// <summary>
    /// Allows the assigned doctor to decline a pending appointment.
    /// </summary>
    Task DoctorRejectAppointmentAsync(Guid appointmentId, Guid doctorId, string reason);

    /// <summary>
    /// Returns a scoped appointment.
    /// </summary>
    Task<AppointmentResponseDto?> GetByIdAsync(Guid appointmentId, Guid requesterId, string requesterRole, Guid? centerId = null);

    /// <summary>
    /// Returns scoped appointments.
    /// </summary>
    Task<PagedResult<AppointmentResponseDto>> GetAppointmentsAsync(Guid requesterId, string requesterRole, Guid? centerId, AppointmentFilterDto filter);

    /// <summary>
    /// Returns upcoming appointments (date >= today, status Pending/Confirmed), sorted ascending.
    /// </summary>
    Task<IReadOnlyList<AppointmentResponseDto>> GetUpcomingAsync(Guid patientId, CancellationToken ct = default);

    /// <summary>
    /// Returns past appointments (date < today or status Completed/Cancelled/NoShow), paginated descending.
    /// </summary>
    Task<PagedResult<AppointmentResponseDto>> GetPastAsync(Guid patientId, int page, int pageSize, CancellationToken ct = default);

    /// <summary>
    /// Marks a confirmed or in-progress appointment as completed by the doctor.
    /// </summary>
    Task MarkCompleteAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default);
}

public class AppointmentAvailabilityService(
    IDoctorScheduleRepository doctorScheduleRepository,
    IAppointmentRepository appointmentRepository,
    IScheduleExceptionRepository scheduleExceptionRepository) : IAppointmentAvailabilityService
{
    /// <inheritdoc />
    public async Task<List<TimeSlot>> GetAvailableSlotsAsync(Guid doctorId, Guid centerId, DateOnly date)
    {
        var schedule = await doctorScheduleRepository.GetByDoctorAndCenterAsync(doctorId, centerId);
        if (schedule is null)
            return [];

        var isException = await scheduleExceptionRepository.ExistsAsync(doctorId, centerId, date);
        if (isException)
            return [];

        var dayName = date.DayOfWeek.ToString();
        if (!schedule.WorkingDays.Contains(dayName, StringComparer.OrdinalIgnoreCase))
            return [];

        var slots = new List<TimeSlot>();
        var current = schedule.StartTime;
        while (current.Add(TimeSpan.FromMinutes(schedule.SlotDuration)) <= schedule.EndTime)
        {
            var inBreak = schedule.BreakStart.HasValue && schedule.BreakEnd.HasValue &&
                          current >= schedule.BreakStart.Value && current < schedule.BreakEnd.Value;
            if (!inBreak)
                slots.Add(new TimeSlot(current, true, schedule.SlotDuration));

            current = current.Add(TimeSpan.FromMinutes(schedule.SlotDuration));
        }

        var existingAppointments = await appointmentRepository.GetByDoctorAndDateAsync(doctorId, date);
        var blockedTimes = existingAppointments
            .Where(a => a.CenterId == centerId && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.AppointmentTime)
            .ToHashSet();

        slots = slots.Select(s => blockedTimes.Contains(s.Time) ? s with { IsAvailable = false } : s).ToList();

        if (date == DateOnly.FromDateTime(DateTime.UtcNow))
        {
            var now = TimeOnly.FromDateTime(DateTime.UtcNow);
            slots = slots.Where(s => s.Time >= now).ToList();
        }

        return slots;
    }

    /// <inheritdoc />
    public async Task<List<DateOnly>> GetAvailableDatesAsync(Guid doctorId, Guid centerId, int daysAhead = 30)
    {
        var result = new List<DateOnly>();
        var start = DateOnly.FromDateTime(DateTime.UtcNow);

        for (var i = 0; i <= daysAhead; i++)
        {
            var date = start.AddDays(i);
            var slots = await GetAvailableSlotsAsync(doctorId, centerId, date);
            if (slots.Any(s => s.IsAvailable))
                result.Add(date);
        }

        return result;
    }
}

public class BookingValidationService(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository healthcareCenterRepository,
    IDoctorRepository doctorRepository,
    IAppointmentAvailabilityService availabilityService) : IBookingValidationService
{
    /// <inheritdoc />
    public async Task ValidateBookingAsync(CreateAppointmentDto dto, Guid patientId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (dto.AppointmentDate < today)
            throw new ValidationException("Appointment date cannot be in the past");

        var center = await healthcareCenterRepository.GetByIdAsync(dto.CenterId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), dto.CenterId);

        if (dto.AppointmentDate > today.AddDays(center.AdvanceBookingDays))
            throw new ValidationException($"Appointments can only be booked up to {center.AdvanceBookingDays} days ahead");

        if (dto.AppointmentDate == today)
        {
            var minTime = TimeOnly.FromDateTime(DateTime.UtcNow.AddHours(2));
            if (dto.AppointmentTime < minTime)
                throw new ValidationException("Appointments require 2+ hours advance booking. Choose later time");
        }

        var doctor = await doctorRepository.GetByIdAsync(dto.DoctorId)
            ?? throw new NotFoundException(nameof(Doctor), dto.DoctorId);

        var hasSameDayAppointment = await appointmentRepository.PatientHasAppointmentTodayAsync(patientId, dto.DoctorId, dto.AppointmentDate);
        if (hasSameDayAppointment)
            throw new ValidationException($"You already have an appointment with Dr. {doctor.FullName} today");

        var slots = await availabilityService.GetAvailableSlotsAsync(dto.DoctorId, dto.CenterId, dto.AppointmentDate);
        var selected = slots.FirstOrDefault(s => s.Time == dto.AppointmentTime);
        if (selected == null || !selected.IsAvailable)
            throw new ValidationException("Selected time slot is no longer available");

        if (center.AutoApproveAppointments)
        {
            // In this implementation, payment is required before direct auto-confirmation can be enforced.
            // Pending status remains until payment flow confirms; this keeps rule explicit.
        }
    }
}

public class AppointmentService(
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository healthcareCenterRepository,
    IDoctorRepository doctorRepository,
    IPatientRepository patientRepository,
    IBookingValidationService bookingValidationService,
    IQueueRepository queueRepository,
    IQueueHubService queueHubService,
    IPushNotificationService pushNotificationService,
    INotificationPreferenceRepository notificationPreferenceRepository,
    IWaitlistService waitlistService,
    IVideoConsultationRepository videoConsultationRepository,
    IUnitOfWork unitOfWork,
    ILogger<AppointmentService> logger,
    MediMind.Application.Features.Admin.IAuditLogger auditLogger,
    IServiceScopeFactory scopeFactory) : IAppointmentService
{
    /// <inheritdoc />
    public async Task<AppointmentResponseDto> BookAppointmentAsync(CreateAppointmentDto dto, Guid patientId)
    {
        await bookingValidationService.ValidateBookingAsync(dto, patientId);
        Appointment created = null!;
        bool requiresPayment = false;

        await unitOfWork.ExecuteTransactionAsync(async () =>
        {
            // Concurrency note:
            // Use a pessimistic lock statement in infrastructure DbContext transaction scope:
            // SELECT ... FOR UPDATE
            // This service re-checks conflict inside the same transaction boundary.
            var hasConflict = await appointmentRepository.HasConflictAsync(dto.DoctorId, dto.CenterId, dto.AppointmentDate, dto.AppointmentTime);
            if (hasConflict)
                throw new ValidationException("Selected time slot is no longer available");

            var center = await healthcareCenterRepository.GetByIdAsync(dto.CenterId)
                ?? throw new NotFoundException(nameof(HealthcareCenter), dto.CenterId);

            requiresPayment = center.RequiresPaymentBeforeConfirmation;

            var appointment = Appointment.Book(
                patientId,
                dto.DoctorId,
                dto.CenterId,
                dto.AppointmentDate,
                dto.AppointmentTime,
                center.SlotDurationMinutes,
                dto.ReasonForVisit,
                dto.Symptoms,
                center.AdvanceBookingDays,
                2,
                dto.AppointmentType);

            if (center.AutoApproveAppointments && !center.RequiresPaymentBeforeConfirmation)
                appointment.Approve(Guid.Empty);

            created = await appointmentRepository.CreateAsync(appointment);

            var consultation = VideoConsultation.Create(created.Id);
            await videoConsultationRepository.CreateAsync(consultation);
        });

        var capturedAppointmentId = created.Id;
        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            try
            {
                var prefRepo = sp.GetRequiredService<INotificationPreferenceRepository>();
                var prefs = await prefRepo.GetByUserIdAsync(patientId);
                if (prefs is null || prefs.AppointmentRemindersPush)
                    await sp.GetRequiredService<IPushNotificationService>()
                        .SendToUserAsync(patientId, "Appointment booked", "Your appointment request was submitted.");
                await sp.GetRequiredService<IQueueHubService>()
                    .BroadcastQueueUpdateAsync(dto.CenterId, new { type = "appointmentBooked", appointmentId = capturedAppointmentId });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification dispatch failed for appointment {AppointmentId}", capturedAppointmentId);
            }
        });

        await auditLogger.LogAsync(AuditActions.AppointmentCreated, patientId, "Patient", dto.CenterId, "Appointment", created.Id);

        var response = await BuildResponseAsync(created);
        if (requiresPayment)
            response = response with { RequiresPayment = true, PaymentInitiationUrl = $"/api/v1/payments/initiate/{created.Id}" };
        return response;
    }

    /// <inheritdoc />
    public async Task CancelAppointmentAsync(Guid appointmentId, Guid requesterId, CancelAppointmentDto dto, string requesterRole, Guid? centerId = null)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (requesterRole == "Patient" && appointment.PatientId != requesterId)
            throw new UnauthorizedException();
        if (requesterRole == "Doctor" && appointment.DoctorId != requesterId)
            throw new UnauthorizedException();
        if (requesterRole == "Admin" && appointment.CenterId != centerId)
            throw new UnauthorizedException();

        if (appointment.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new ValidationException("Only pending or confirmed appointments can be cancelled");

        var center = await healthcareCenterRepository.GetByIdAsync(appointment.CenterId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), appointment.CenterId);

        if (appointment.Status == AppointmentStatus.Confirmed)
        {
            var minAllowed = appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime).AddHours(-center.CancellationHours);
            if (DateTime.UtcNow > minAllowed)
                throw new ValidationException("Too close to appointment time");
        }

        var freedDate = appointment.AppointmentDate;
        var doctorId = appointment.DoctorId;
        var patientId = appointment.PatientId;
        var appointmentCenterId = appointment.CenterId;

        if (appointment.VideoConsultation?.Status is VideoConsultationStatus.Scheduled or VideoConsultationStatus.InProgress)
            appointment.VideoConsultation.Cancel();

        appointment.Cancel(requesterId, dto.CancellationReason, center.CancellationHours);
        await unitOfWork.SaveChangesAsync();

        await auditLogger.LogAsync(AuditActions.AppointmentCancelled, requesterId, requesterRole, appointmentCenterId, "Appointment", appointmentId);

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            var sp = scope.ServiceProvider;
            try
            {
                var prefRepo = sp.GetRequiredService<INotificationPreferenceRepository>();
                var push = sp.GetRequiredService<IPushNotificationService>();
                var prefs = await prefRepo.GetByUserIdAsync(patientId);
                if (prefs is null || prefs.AppointmentRemindersPush)
                    await push.SendToUserAsync(patientId, "Appointment cancelled", "Your appointment was cancelled.");

                var centerWithAdmins = await sp.GetRequiredService<IHealthcareCenterRepository>()
                    .GetWithAdminsAsync(appointmentCenterId);
                if (centerWithAdmins is not null)
                {
                    foreach (var admin in centerWithAdmins.Admins)
                        await push.SendToUserAsync(admin.Id, "Appointment cancelled",
                            $"An appointment on {freedDate:MMM dd} was cancelled by the patient.");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cancellation notification failed for appointment {AppointmentId}", appointmentId);
            }
        });

        _ = Task.Run(async () =>
        {
            using var scope = scopeFactory.CreateScope();
            try
            {
                await scope.ServiceProvider.GetRequiredService<IWaitlistService>()
                    .NotifyWaitlistAsync(doctorId, appointmentCenterId, freedDate);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Waitlist notification failed after cancellation of appointment {AppointmentId}", appointmentId);
            }
        });
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto> RescheduleAppointmentAsync(Guid appointmentId, Guid patientId, RescheduleAppointmentDto dto)
    {
        var existing = await appointmentRepository.GetByIdForPatientAsync(appointmentId, patientId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (!existing.CanReschedule)
            throw new ValidationException("Appointment cannot be rescheduled");

        var appointmentDateTime = existing.AppointmentDate.ToDateTime(existing.AppointmentTime);
        if (appointmentDateTime <= DateTime.UtcNow.AddHours(2))
            throw new ValidationException("Too close to appointment time. Contact healthcare center to reschedule.");

        if (existing.VideoConsultation?.Status is VideoConsultationStatus.Scheduled or VideoConsultationStatus.InProgress)
            existing.VideoConsultation.Cancel();

        existing.IncrementRescheduleCount();
        existing.Cancel(patientId, dto.Reason ?? "Rescheduled by patient", 0);

        var createDto = new CreateAppointmentDto(
            existing.CenterId,
            existing.DoctorId,
            dto.NewDate,
            dto.NewTime,
            existing.ReasonForVisit,
            existing.Symptoms,
            existing.AppointmentType);

        await bookingValidationService.ValidateBookingAsync(createDto, patientId);

        var center = await healthcareCenterRepository.GetByIdAsync(existing.CenterId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), existing.CenterId);

        var newAppointment = Appointment.Book(
            patientId,
            existing.DoctorId,
            existing.CenterId,
            dto.NewDate,
            dto.NewTime,
            center.SlotDurationMinutes,
            existing.ReasonForVisit,
            existing.Symptoms,
            center.AdvanceBookingDays,
            2,
            existing.AppointmentType);

        newAppointment.LinkToOriginal(existing.Id);
        if (center.AutoApproveAppointments && !center.RequiresPaymentBeforeConfirmation)
            newAppointment.Approve(Guid.Empty);

        await appointmentRepository.CreateAsync(newAppointment);

        var newConsultation = VideoConsultation.Create(newAppointment.Id);
        await videoConsultationRepository.CreateAsync(newConsultation);

        await unitOfWork.SaveChangesAsync();

        await auditLogger.LogAsync(AuditActions.AppointmentRescheduled, patientId, "Patient", newAppointment.CenterId, "Appointment", newAppointment.Id);

        return await BuildResponseAsync(newAppointment);
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto> ApproveAppointmentAsync(Guid appointmentId, Guid adminId, Guid adminCenterId)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.CenterId != adminCenterId)
            throw new UnauthorizedException();
        if (appointment.Status != AppointmentStatus.Pending)
            throw new ValidationException("Only pending appointments can be approved");

        appointment.Approve(adminId);
        await unitOfWork.SaveChangesAsync();

        var existingQueue = await queueRepository.GetByAppointmentAsync(appointment.Id);
        if (existingQueue is null)
        {
            var sameDayCount = (await appointmentRepository.GetByCenterAndDateAsync(appointment.CenterId, appointment.AppointmentDate))
                .Count(a => a.Status == AppointmentStatus.Confirmed);
            var queueEntry = new QueueEntry(appointment.Id, appointment.CenterId, appointment.AppointmentDate, sameDayCount, appointment.DurationMinutes);
            await queueRepository.AddAsync(queueEntry);
            await unitOfWork.SaveChangesAsync();
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await pushNotificationService.SendToUserAsync(appointment.PatientId, "Appointment confirmed", "Your appointment has been approved.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Approval notification failed for appointment {AppointmentId}", appointmentId);
            }
        });

        await auditLogger.LogAsync(AuditActions.AppointmentApproved, adminId, "Admin", adminCenterId, "Appointment", appointmentId);
        return await BuildResponseAsync(appointment);
    }

    /// <inheritdoc />
    public async Task RejectAppointmentAsync(Guid appointmentId, Guid adminId, Guid adminCenterId, string reason)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.CenterId != adminCenterId)
            throw new UnauthorizedException();
        if (appointment.Status != AppointmentStatus.Pending)
            throw new ValidationException("Only pending appointments can be rejected");

        appointment.Reject(adminId, reason);
        await unitOfWork.SaveChangesAsync();

        var patientId = appointment.PatientId;
        _ = Task.Run(async () =>
        {
            try
            {
                var body = string.IsNullOrWhiteSpace(reason)
                    ? "Your appointment request was not approved."
                    : $"Your appointment was rejected: {reason}";
                await pushNotificationService.SendToUserAsync(patientId, "Appointment not approved", body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Rejection notification failed for appointment {AppointmentId}", appointmentId);
            }
        });

        await auditLogger.LogAsync(AuditActions.AppointmentRejected, adminId, "Admin", adminCenterId, "Appointment", appointmentId);
    }

    /// <inheritdoc />
    public async Task DoctorRejectAppointmentAsync(Guid appointmentId, Guid doctorId, string reason)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.DoctorId != doctorId)
            throw new UnauthorizedException();
        if (appointment.Status != AppointmentStatus.Pending)
            throw new ValidationException("Only pending appointments can be declined");

        appointment.Reject(doctorId, reason);
        await unitOfWork.SaveChangesAsync();

        var patientId = appointment.PatientId;
        _ = Task.Run(async () =>
        {
            try
            {
                var body = string.IsNullOrWhiteSpace(reason)
                    ? "Your appointment was declined by the doctor."
                    : $"Your appointment was declined: {reason}";
                await pushNotificationService.SendToUserAsync(patientId, "Appointment declined", body);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Doctor rejection notification failed for appointment {AppointmentId}", appointmentId);
            }
        });

        await auditLogger.LogAsync(AuditActions.AppointmentRejected, doctorId, "Doctor", appointment.CenterId, "Appointment", appointmentId);
    }

    /// <inheritdoc />
    public async Task<AppointmentResponseDto?> GetByIdAsync(Guid appointmentId, Guid requesterId, string requesterRole, Guid? centerId = null)
    {
        Appointment? appointment = requesterRole switch
        {
            "Patient" => await appointmentRepository.GetByIdForPatientAsync(appointmentId, requesterId),
            _ => await appointmentRepository.GetByIdAsync(appointmentId)
        };

        if (appointment is null)
            return null;

        if (requesterRole == "Doctor" && appointment.DoctorId != requesterId)
            throw new UnauthorizedException();
        if (requesterRole == "Admin" && appointment.CenterId != centerId)
            throw new UnauthorizedException();

        return await BuildResponseAsync(appointment);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AppointmentResponseDto>> GetAppointmentsAsync(Guid requesterId, string requesterRole, Guid? centerId, AppointmentFilterDto filter)
    {
        PagedResult<Appointment> result = requesterRole switch
        {
            "Patient" => await appointmentRepository.GetByPatientAsync(requesterId, filter),
            "Doctor" => await appointmentRepository.GetByDoctorAsync(requesterId, centerId ?? Guid.Empty, filter),
            "Admin" => await appointmentRepository.GetByCenterAsync(centerId ?? Guid.Empty, filter),
            _ => new PagedResult<Appointment>([], filter.Page, filter.PageSize, 0)
        };

        var dtos = new List<AppointmentResponseDto>(result.Items.Count);
        foreach (var appointment in result.Items)
            dtos.Add(await BuildResponseAsync(appointment));

        return new PagedResult<AppointmentResponseDto>(dtos, result.Page, result.PageSize, result.TotalCount);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentResponseDto>> GetUpcomingAsync(Guid patientId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var upcomingStatuses = new[] { AppointmentStatus.Pending, AppointmentStatus.Confirmed };
        var all = await appointmentRepository.GetByPatientAsync(patientId, ct);
        var upcoming = all
            .Where(a => a.AppointmentDate >= today && upcomingStatuses.Contains(a.Status))
            .OrderBy(a => a.AppointmentDate).ThenBy(a => a.AppointmentTime)
            .ToList();

        var result = new List<AppointmentResponseDto>(upcoming.Count);
        foreach (var a in upcoming)
            result.Add(await BuildResponseAsync(a));
        return result;
    }

    /// <inheritdoc />
    public async Task<PagedResult<AppointmentResponseDto>> GetPastAsync(Guid patientId, int page, int pageSize, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var pastStatuses = new[] { AppointmentStatus.Completed, AppointmentStatus.Cancelled, AppointmentStatus.NoShow };
        var all = await appointmentRepository.GetByPatientAsync(patientId, ct);
        var past = all
            .Where(a => a.AppointmentDate < today || pastStatuses.Contains(a.Status))
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.AppointmentTime)
            .ToList();

        var totalCount = past.Count;
        var boundedPage = Math.Max(page, 1);
        var boundedSize = Math.Clamp(pageSize, 1, 100);
        var paged = past.Skip((boundedPage - 1) * boundedSize).Take(boundedSize).ToList();

        var dtos = new List<AppointmentResponseDto>(paged.Count);
        foreach (var a in paged)
            dtos.Add(await BuildResponseAsync(a));
        return new PagedResult<AppointmentResponseDto>(dtos, boundedPage, boundedSize, totalCount);
    }

    private async Task<AppointmentResponseDto> BuildResponseAsync(Appointment appointment)
    {
        var full = appointment.Patient is null
            ? await appointmentRepository.GetByIdAsync(appointment.Id) ?? appointment
            : appointment;

        var center = full.Center;
        var canCancel = full.IsCancellable(center?.CancellationHours ?? 2);
        var queue = full.QueueEntry;
        var appointmentActive = full.Status is not (AppointmentStatus.Cancelled or AppointmentStatus.NoShow or AppointmentStatus.Completed);
        var canInitiateVideo = appointmentActive
            && (full.VideoConsultation == null
                || full.VideoConsultation.Status is (VideoConsultationStatus.Scheduled or VideoConsultationStatus.InProgress));
        var canChat = appointmentActive;
        var latestPayment = full.Payments.OrderByDescending(p => p.CreatedAt).FirstOrDefault();

        var doctorCenterLink = full.Doctor?.DoctorHealthcareCenters
            .FirstOrDefault(dhc => dhc.CenterId == full.CenterId);
        var consultationFee = doctorCenterLink?.ConsultationFee;
        var serviceFee = consultationFee.HasValue ? Math.Round(consultationFee.Value * 0.02m, 2) : (decimal?)null;
        var totalAmount = consultationFee.HasValue ? consultationFee.Value + serviceFee!.Value : (decimal?)null;

        return new AppointmentResponseDto(
            Id: full.Id,
            Status: full.Status.ToString(),
            DateTime: full.AppointmentDate.ToDateTime(full.AppointmentTime),
            DurationMinutes: full.DurationMinutes,
            Reason: full.ReasonForVisit,
            Symptoms: full.Symptoms,
            BookingDate: full.BookingDate,
            PatientId: full.PatientId,
            PatientName: full.Patient?.FullName ?? string.Empty,
            PatientPhone: full.Patient?.PhoneNumber,
            DoctorId: full.DoctorId,
            DoctorName: full.Doctor?.FullName ?? string.Empty,
            DoctorSpecialization: full.Doctor?.Specialization,
            DoctorAvatarUrl: full.Doctor?.ProfileImageUrl,
            CenterId: full.CenterId,
            CenterName: center?.CenterName ?? string.Empty,
            CenterAddress: center?.Address,
            CenterPhone: center?.PhoneNumber,
            CenterLatitude: center?.Latitude.HasValue == true ? (double?)Convert.ToDouble(center.Latitude) : null,
            CenterLongitude: center?.Longitude.HasValue == true ? (double?)Convert.ToDouble(center.Longitude) : null,
            Type: full.AppointmentType.ToString(),
            CanCancel: canCancel,
            CanReschedule: full.CanReschedule,
            QueueNumber: queue?.Position,
            EstimatedWaitMinutes: queue?.EstimatedWaitTimeMinutes,
            CanInitiateVideoConsultation: canInitiateVideo,
            CanChat: canChat,
            VideoConsultationId: full.VideoConsultation?.ConsultationId,
            VideoConsultationStatus: full.VideoConsultation?.Status.ToString(),
            CancellationPolicyHours: center?.CancellationHours ?? 2,
            PaymentId: latestPayment?.Id,
            PaymentStatus: latestPayment?.Status.ToString(),
            ConsultationFee: consultationFee,
            ServiceFee: serviceFee,
            TotalAmount: totalAmount);
    }

    public async Task MarkCompleteAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.DoctorId != doctorId)
            throw new UnauthorizedException();

        appointment.MarkCompleted();

        if (appointment.VideoConsultation?.Status is VideoConsultationStatus.Scheduled or VideoConsultationStatus.InProgress)
            appointment.VideoConsultation.Cancel();

        await unitOfWork.SaveChangesAsync(ct);
    }
}

// ─── Waitlist Service ─────────────────────────────────────────────────────────

public interface IWaitlistService
{
    Task<WaitlistResponseDto> SubscribeAsync(Guid patientId, WaitlistSubscribeDto dto, CancellationToken ct = default);
    Task UnsubscribeAsync(Guid subscriptionId, Guid patientId, CancellationToken ct = default);
    Task NotifyWaitlistAsync(Guid doctorId, Guid centerId, DateOnly freedDate, CancellationToken ct = default);
}

public class WaitlistService(
    IWaitlistSubscriptionRepository waitlistRepository,
    IDoctorRepository doctorRepository,
    IPushNotificationService pushNotificationService,
    IUnitOfWork unitOfWork,
    ILogger<WaitlistService> logger) : IWaitlistService
{
    public async Task<WaitlistResponseDto> SubscribeAsync(Guid patientId, WaitlistSubscribeDto dto, CancellationToken ct = default)
    {
        var existing = await waitlistRepository.GetActiveByPatientDoctorCenterAsync(patientId, dto.DoctorId, dto.CenterId, ct);
        if (existing is not null)
            return MapToDto(existing);

        var subscription = WaitlistSubscription.Create(patientId, dto.DoctorId, dto.CenterId, dto.PreferredDateFrom, dto.PreferredDateTo);
        await waitlistRepository.AddAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToDto(subscription);
    }

    public async Task UnsubscribeAsync(Guid subscriptionId, Guid patientId, CancellationToken ct = default)
    {
        var subscription = await waitlistRepository.GetByIdAsync(subscriptionId, patientId, ct)
            ?? throw new NotFoundException(nameof(WaitlistSubscription), subscriptionId);
        subscription.Deactivate();
        await waitlistRepository.UpdateAsync(subscription, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task NotifyWaitlistAsync(Guid doctorId, Guid centerId, DateOnly freedDate, CancellationToken ct = default)
    {
        var subscribers = await waitlistRepository.GetActiveByDoctorAndCenterAsync(doctorId, centerId, ct);
        var eligible = subscribers.Where(s => freedDate >= s.PreferredDateFrom && freedDate <= s.PreferredDateTo).ToList();
        if (eligible.Count == 0) return;

        var doctor = await doctorRepository.GetByIdAsync(doctorId);
        var doctorName = doctor?.FullName ?? "your doctor";

        foreach (var sub in eligible)
        {
            try
            {
                await pushNotificationService.SendToUserAsync(
                    sub.PatientId,
                    "Appointment slot available",
                    $"A slot has opened for Dr. {doctorName} on {freedDate:MMM dd}. Book now!",
                    new Dictionary<string, string>
                    {
                        ["doctorId"] = doctorId.ToString(),
                        ["centerId"] = centerId.ToString(),
                        ["date"] = freedDate.ToString("yyyy-MM-dd")
                    },
                    ct);
                sub.MarkNotified();
                await waitlistRepository.UpdateAsync(sub, ct);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to notify waitlist subscriber {PatientId}", sub.PatientId);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }

    private static WaitlistResponseDto MapToDto(WaitlistSubscription s) =>
        new(s.Id, s.PatientId, s.DoctorId, s.CenterId, s.PreferredDateFrom, s.PreferredDateTo, s.IsActive, s.NotifiedAt);
}
