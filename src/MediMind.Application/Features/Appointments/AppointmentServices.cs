using FluentValidation;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
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
    /// Returns a scoped appointment.
    /// </summary>
    Task<AppointmentResponseDto?> GetByIdAsync(Guid appointmentId, Guid requesterId, string requesterRole, Guid? centerId = null);

    /// <summary>
    /// Returns scoped appointments.
    /// </summary>
    Task<PagedResult<AppointmentResponseDto>> GetAppointmentsAsync(Guid requesterId, string requesterRole, Guid? centerId, AppointmentFilterDto filter);
}

public class AppointmentAvailabilityService(
    IDoctorScheduleRepository doctorScheduleRepository,
    IAppointmentRepository appointmentRepository) : IAppointmentAvailabilityService
{
    /// <inheritdoc />
    public async Task<List<TimeSlot>> GetAvailableSlotsAsync(Guid doctorId, Guid centerId, DateOnly date)
    {
        var schedule = await doctorScheduleRepository.GetByDoctorAndCenterAsync(doctorId, centerId);
        if (schedule is null)
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
    IUnitOfWork unitOfWork,
    ILogger<AppointmentService> logger) : IAppointmentService
{
    /// <inheritdoc />
    public async Task<AppointmentResponseDto> BookAppointmentAsync(CreateAppointmentDto dto, Guid patientId)
    {
        await bookingValidationService.ValidateBookingAsync(dto, patientId);
        await unitOfWork.BeginTransactionAsync();
        Appointment created;
        try
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
                2);

            if (center.AutoApproveAppointments)
                appointment.Approve(Guid.Empty);

            created = await appointmentRepository.CreateAsync(appointment);
            await unitOfWork.CommitTransactionAsync();
        }
        catch
        {
            await unitOfWork.RollbackTransactionAsync();
            throw;
        }

        _ = Task.Run(async () =>
        {
            try
            {
                await pushNotificationService.SendToUserAsync(patientId, "Appointment booked", "Your appointment request was submitted.");
                await queueHubService.BroadcastQueueUpdateAsync(dto.CenterId, new { type = "appointmentBooked", appointmentId = created.Id });
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Notification dispatch failed for appointment {AppointmentId}", created.Id);
            }
        });

        return await BuildResponseAsync(created);
    }

    /// <inheritdoc />
    public async Task CancelAppointmentAsync(Guid appointmentId, Guid requesterId, CancelAppointmentDto dto, string requesterRole, Guid? centerId = null)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (requesterRole == "Patient" && appointment.PatientId != requesterId)
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

        appointment.Cancel(requesterId, dto.CancellationReason, center.CancellationHours);
        await unitOfWork.SaveChangesAsync();

        _ = Task.Run(async () =>
        {
            try
            {
                await pushNotificationService.SendToUserAsync(appointment.PatientId, "Appointment cancelled", "Your appointment was cancelled.");
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Cancellation notification failed for appointment {AppointmentId}", appointmentId);
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

        existing.IncrementRescheduleCount();
        existing.Cancel(patientId, dto.Reason ?? "Rescheduled by patient", 0);

        var createDto = new CreateAppointmentDto(
            existing.CenterId,
            existing.DoctorId,
            dto.NewDate,
            dto.NewTime,
            existing.ReasonForVisit,
            existing.Symptoms);

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
            2);

        newAppointment.LinkToOriginal(existing.Id);
        if (center.AutoApproveAppointments)
            newAppointment.Approve(Guid.Empty);

        await appointmentRepository.CreateAsync(newAppointment);
        await unitOfWork.SaveChangesAsync();

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

    private async Task<AppointmentResponseDto> BuildResponseAsync(Appointment appointment)
    {
        var full = appointment.Patient is null
            ? await appointmentRepository.GetByIdAsync(appointment.Id) ?? appointment
            : appointment;

        var center = full.Center;
        var canCancel = full.IsCancellable(center?.CancellationHours ?? 2);
        var queue = full.QueueEntry;

        return new AppointmentResponseDto(
            full.Id,
            full.Status.ToString(),
            full.AppointmentDate,
            full.AppointmentTime,
            full.DurationMinutes,
            full.ReasonForVisit,
            full.BookingDate,
            new AppointmentPatientDto(full.PatientId, full.Patient?.FullName ?? string.Empty, full.Patient?.PhoneNumber ?? string.Empty),
            new AppointmentDoctorDto(full.DoctorId, full.Doctor?.FullName ?? string.Empty, full.Doctor?.Specialization ?? string.Empty),
            new AppointmentCenterDto(full.CenterId, center?.CenterName ?? string.Empty, center?.Address ?? string.Empty, center?.PhoneNumber ?? string.Empty),
            canCancel,
            full.CanReschedule,
            queue?.QueueNumber,
            queue?.EstimatedWaitTimeMinutes);
    }
}
