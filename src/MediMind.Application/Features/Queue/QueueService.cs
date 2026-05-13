using System.Collections.Generic;
using MediMind.Application.Notifications;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Features.Queue;

/// <summary>
/// Queue management service with real-time notifications.
/// </summary>
public interface IQueueService
{
    /// <summary>Calls the next waiting patient for a center.</summary>
    Task<QueueItemDto> CallNextPatientAsync(Guid centerId, Guid adminId, string? roomNumber = null);

    /// <summary>Marks a patient as arrived and starts consultation.</summary>
    Task<QueueItemDto> MarkPatientArrivedAsync(Guid queueId, Guid adminId);

    /// <summary>Marks consultation as complete.</summary>
    Task<QueueItemDto> MarkConsultationCompleteAsync(Guid queueId, Guid adminId);

    /// <summary>Marks queue entry as no-show.</summary>
    Task<QueueItemDto> MarkNoShowAsync(Guid queueId, Guid adminId);

    /// <summary>Inserts emergency patient at top of queue.</summary>
    Task<QueueItemDto> InsertEmergencyPatientAsync(Guid appointmentId, Guid adminId);

    /// <summary>Gets queue status for a patient appointment.</summary>
    Task<PatientQueueStatusDto> GetQueueStatusAsync(Guid appointmentId, Guid patientId);

    /// <summary>Gets admin dashboard queue view for center/date.</summary>
    Task<AdminQueueDashboardDto> GetCenterDashboardAsync(Guid centerId, DateOnly date, Guid adminId);

    /// <summary>Generates queue for a center/date manually.</summary>
    Task<AdminQueueDashboardDto> GenerateQueueForCenterAsync(Guid centerId, DateOnly date, Guid requesterId);

    /// <summary>Generates queues for all centers for provided date.</summary>
    Task GenerateQueuesForAllCentersAsync(DateOnly date);
}

public class QueueService(
    IQueueRepository queueRepository,
    IAppointmentRepository appointmentRepository,
    IHealthcareCenterRepository healthcareCenterRepository,
    INotificationService notificationService,
    IMemoryCache cache,
    IUnitOfWork unitOfWork,
    ILogger<QueueService> logger) : IQueueService
{
    /// <inheritdoc />
    public async Task<QueueItemDto> CallNextPatientAsync(Guid centerId, Guid adminId, string? roomNumber = null)
    {
        await EnsureAdminBelongsToCenter(centerId, adminId);
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var next = await queueRepository.GetNextWaitingAsync(centerId, date)
            ?? throw new NotFoundException("Queue", $"No waiting patients for center {centerId} on {date}");

        next.CallPatient(roomNumber);
        await unitOfWork.SaveChangesAsync();

        await queueRepository.RecalculatePositionsAsync(centerId, date);
        await unitOfWork.SaveChangesAsync();

        var appointment = await appointmentRepository.GetByIdAsync(next.AppointmentId)
            ?? throw new NotFoundException(nameof(Appointment), next.AppointmentId);

        var center = await healthcareCenterRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var effectiveRoom = roomNumber ?? "consultation room";
        var tpl = NotificationTemplates.QueueCalledNow(effectiveRoom, center.CenterName);
        await notificationService.SendPushAsync(
            appointment.PatientId,
            tpl.Title,
            tpl.Body,
            new Dictionary<string, string> { ["queueId"] = next.Id.ToString() });
        if (!string.IsNullOrWhiteSpace(appointment.Patient.PhoneNumber))
            await notificationService.SendSmsAsync(appointment.Patient.PhoneNumber, tpl.SmsMessage);

        var calledItem = MapQueueItem(next, appointment);
        await notificationService.BroadcastQueueEventAsync(centerId, "NextPatientCalled", new
        {
            queueId = calledItem.QueueId,
            queueNumber = calledItem.QueueNumber,
            patientName = calledItem.PatientName,
            roomNumber = next.RoomNumber
        });

        var dashboard = await BuildDashboard(centerId, date);
        await notificationService.BroadcastQueueRefreshAsync(centerId, dashboard);

        return calledItem;
    }

    /// <inheritdoc />
    public async Task<QueueItemDto> MarkPatientArrivedAsync(Guid queueId, Guid adminId)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new NotFoundException("Queue", queueId);

        await EnsureAdminBelongsToCenter(queue.CenterId, adminId);

        queue.StartConsultation();
        await unitOfWork.SaveChangesAsync();

        var appointment = await appointmentRepository.GetByIdAsync(queue.AppointmentId)
            ?? throw new NotFoundException(nameof(Appointment), queue.AppointmentId);

        await notificationService.BroadcastQueueEventAsync(queue.CenterId, "PatientStatusChanged", new
        {
            appointmentId = queue.AppointmentId,
            newStatus = queue.Status.ToString(),
            message = "Consultation started"
        });

        return MapQueueItem(queue, appointment);
    }

    /// <inheritdoc />
    public async Task<QueueItemDto> MarkConsultationCompleteAsync(Guid queueId, Guid adminId)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new NotFoundException("Queue", queueId);

        await EnsureAdminBelongsToCenter(queue.CenterId, adminId);

        queue.CompleteConsultation();
        await unitOfWork.SaveChangesAsync();

        await queueRepository.RecalculatePositionsAsync(queue.CenterId, queue.QueueDate);
        await unitOfWork.SaveChangesAsync();

        await NotifyWaitingPatients(queue.CenterId, queue.QueueDate);

        var dashboard = await BuildDashboard(queue.CenterId, queue.QueueDate);
        await notificationService.BroadcastQueueRefreshAsync(queue.CenterId, dashboard);

        var appointment = await appointmentRepository.GetByIdAsync(queue.AppointmentId)
            ?? throw new NotFoundException(nameof(Appointment), queue.AppointmentId);

        return MapQueueItem(queue, appointment);
    }

    /// <inheritdoc />
    public async Task<QueueItemDto> MarkNoShowAsync(Guid queueId, Guid adminId)
    {
        var queue = await queueRepository.GetByIdAsync(queueId)
            ?? throw new NotFoundException("Queue", queueId);

        await EnsureAdminBelongsToCenter(queue.CenterId, adminId);

        queue.MarkMissed();
        await unitOfWork.SaveChangesAsync();

        await queueRepository.RecalculatePositionsAsync(queue.CenterId, queue.QueueDate);
        await unitOfWork.SaveChangesAsync();

        await NotifyWaitingPatients(queue.CenterId, queue.QueueDate);

        var dashboard = await BuildDashboard(queue.CenterId, queue.QueueDate);
        await notificationService.BroadcastQueueRefreshAsync(queue.CenterId, dashboard);

        var appointment = await appointmentRepository.GetByIdAsync(queue.AppointmentId)
            ?? throw new NotFoundException(nameof(Appointment), queue.AppointmentId);

        return MapQueueItem(queue, appointment);
    }

    /// <inheritdoc />
    public async Task<QueueItemDto> InsertEmergencyPatientAsync(Guid appointmentId, Guid adminId)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.Status != AppointmentStatus.Confirmed)
            throw new DomainException("Emergency insert requires confirmed appointment.");

        await EnsureAdminBelongsToCenter(appointment.CenterId, adminId);

        var todayEntries = (await queueRepository.GetCenterQueueAsync(appointment.CenterId, appointment.AppointmentDate)).ToList();
        var center = await healthcareCenterRepository.GetByIdAsync(appointment.CenterId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), appointment.CenterId);

        foreach (var e in todayEntries.Where(e => e.Status == QueueStatus.Waiting))
            e.UpdatePosition(e.Position + 1, center.SlotDurationMinutes);

        var emergency = new QueueEntry(appointment.Id, appointment.CenterId, appointment.AppointmentDate, 1, center.SlotDurationMinutes);
        await queueRepository.CreateAsync(emergency);
        await unitOfWork.SaveChangesAsync();

        await queueRepository.RecalculatePositionsAsync(appointment.CenterId, appointment.AppointmentDate);
        await unitOfWork.SaveChangesAsync();

        await NotifyWaitingPatients(appointment.CenterId, appointment.AppointmentDate);
        var dashboard = await BuildDashboard(appointment.CenterId, appointment.AppointmentDate);
        await notificationService.BroadcastQueueRefreshAsync(appointment.CenterId, dashboard);

        return MapQueueItem(emergency, appointment);
    }

    /// <inheritdoc />
    public async Task<PatientQueueStatusDto> GetQueueStatusAsync(Guid appointmentId, Guid patientId)
    {
        var appointment = await appointmentRepository.GetByIdAsync(appointmentId)
            ?? throw new NotFoundException(nameof(Appointment), appointmentId);

        if (appointment.PatientId != patientId)
            throw new UnauthorizedException();

        var queue = await queueRepository.GetByAppointmentIdAsync(appointmentId)
            ?? throw new NotFoundException("Queue", appointmentId);

        var center = appointment.Center;
        var doctor = appointment.Doctor;

        return new PatientQueueStatusDto(
            queue.Id,
            queue.QueueNumber,
            queue.Position,
            queue.EstimatedWaitTimeMinutes,
            FormatWaitTime(queue.EstimatedWaitTimeMinutes),
            queue.Status.ToString(),
            BuildStatusMessage(queue),
            new QueueCenterInfoDto(center.CenterName, center.Address),
            new QueueDoctorInfoDto(doctor.FullName, doctor.Specialization),
            appointment.AppointmentTime);
    }

    /// <inheritdoc />
    public async Task<AdminQueueDashboardDto> GetCenterDashboardAsync(Guid centerId, DateOnly date, Guid adminId)
    {
        await EnsureAdminBelongsToCenter(centerId, adminId);
        return await BuildDashboard(centerId, date);
    }

    /// <inheritdoc />
    public async Task<AdminQueueDashboardDto> GenerateQueueForCenterAsync(Guid centerId, DateOnly date, Guid requesterId)
    {
        await EnsureAdminBelongsToCenter(centerId, requesterId);
        await GenerateQueueForCenterInternal(centerId, date);
        return await BuildDashboard(centerId, date);
    }

    /// <inheritdoc />
    public async Task GenerateQueuesForAllCentersAsync(DateOnly date)
    {
        var appointments = await appointmentRepository.GetConfirmedForQueueGenerationAsync(date);
        var grouped = appointments
            .Where(a => a.QueueEntry is null)
            .GroupBy(a => a.CenterId)
            .ToList();

        foreach (var group in grouped)
        {
            await GenerateQueueForCenterInternal(group.Key, date, group.ToList());
        }
    }

    private async Task GenerateQueueForCenterInternal(Guid centerId, DateOnly date, List<Appointment>? preloaded = null)
    {
        var exists = await queueRepository.ExistsForDateAsync(centerId, date);
        if (exists)
        {
            logger.LogInformation("Queue already exists for center {CenterId} on {Date}", centerId, date);
            return;
        }

        var center = await healthcareCenterRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var appointments = preloaded ?? (await appointmentRepository.GetConfirmedForQueueGenerationAsync(date))
            .Where(a => a.CenterId == centerId && a.QueueEntry is null)
            .ToList();

        var ordered = appointments.OrderBy(a => a.AppointmentTime).ToList();
        var entries = new List<QueueEntry>(ordered.Count);

        for (var i = 0; i < ordered.Count; i++)
        {
            entries.Add(new QueueEntry(
                ordered[i].Id,
                centerId,
                date,
                i + 1,
                center.SlotDurationMinutes));
        }

        await queueRepository.BulkCreateAsync(entries);
        await unitOfWork.SaveChangesAsync();

        foreach (var item in ordered.Select((a, index) => new { Appointment = a, Position = index + 1, Wait = index * center.SlotDurationMinutes }))
        {
            var text = $"You are #{item.Position} in today's queue at {center.CenterName}. Estimated wait: {FormatWaitTime(item.Wait)}.";
            await notificationService.SendPushAsync(item.Appointment.PatientId, "Queue generated", text);
            if (!string.IsNullOrWhiteSpace(item.Appointment.Patient.PhoneNumber))
                await notificationService.SendSmsAsync(item.Appointment.Patient.PhoneNumber, text);
        }

        var dashboard = await BuildDashboard(centerId, date);
        await notificationService.BroadcastQueueRefreshAsync(centerId, dashboard);
    }

    private async Task EnsureAdminBelongsToCenter(Guid centerId, Guid adminId)
    {
        var center = await healthcareCenterRepository.GetWithAdminsAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var isMember = center.Admins.Any(a => a.Id == adminId);
        if (!isMember)
            throw new UnauthorizedException();
    }

    private async Task NotifyWaitingPatients(Guid centerId, DateOnly date)
    {
        var entries = await queueRepository.GetCenterQueueAsync(centerId, date);
        foreach (var entry in entries.Where(e => e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Called))
        {
            var appointment = entry.Appointment;
            if (appointment is null)
                continue;

            // Always send SignalR position update
            var status = new PatientQueueStatusDto(
                entry.Id,
                entry.QueueNumber,
                entry.Position,
                entry.EstimatedWaitTimeMinutes,
                FormatWaitTime(entry.EstimatedWaitTimeMinutes),
                entry.Status.ToString(),
                BuildStatusMessage(entry),
                new QueueCenterInfoDto(appointment.Center.CenterName, appointment.Center.Address),
                new QueueDoctorInfoDto(appointment.Doctor.FullName, appointment.Doctor.Specialization),
                appointment.AppointmentTime);

            await notificationService.SendQueueUpdateToPatientAsync(appointment.PatientId, status);

            if (entry.Status != QueueStatus.Waiting)
                continue;

            // One-time push+SMS when 3 positions away
            if (entry.Position == 3)
            {
                var key = $"queue_soon_{entry.Id}_{date:yyyy-MM-dd}";
                if (!cache.TryGetValue(key, out _))
                {
                    cache.Set(key, true, TimeSpan.FromHours(12));
                    var tpl = NotificationTemplates.QueueCallingSoon(entry.Position);
                    await notificationService.SendPushAsync(appointment.PatientId, tpl.Title, tpl.Body,
                        new Dictionary<string, string> { ["queueId"] = entry.Id.ToString() });
                    if (!string.IsNullOrWhiteSpace(appointment.Patient?.PhoneNumber))
                        await notificationService.SendSmsAsync(appointment.Patient.PhoneNumber, tpl.SmsMessage);
                }
            }

            // One-time push+SMS when patient is next (position 1)
            if (entry.Position == 1)
            {
                var key = $"queue_next_{entry.Id}_{date:yyyy-MM-dd}";
                if (!cache.TryGetValue(key, out _))
                {
                    cache.Set(key, true, TimeSpan.FromHours(12));
                    var tpl = NotificationTemplates.QueueYouAreNext();
                    await notificationService.SendPushAsync(appointment.PatientId, tpl.Title, tpl.Body,
                        new Dictionary<string, string> { ["queueId"] = entry.Id.ToString() });
                    if (!string.IsNullOrWhiteSpace(appointment.Patient?.PhoneNumber))
                        await notificationService.SendSmsAsync(appointment.Patient.PhoneNumber, tpl.SmsMessage);
                }
            }
        }
    }

    private async Task<AdminQueueDashboardDto> BuildDashboard(Guid centerId, DateOnly date)
    {
        var center = await healthcareCenterRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var entries = (await queueRepository.GetCenterQueueAsync(centerId, date)).ToList();
        var waiting = entries.Where(e => e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Called).OrderBy(e => e.Position).ToList();
        var inConsult = entries.FirstOrDefault(e => e.Status == QueueStatus.InConsultation);

        var completedDurations = entries
            .Where(e => e.ConsultationStartTime.HasValue && e.ConsultationEndTime.HasValue)
            .Select(e => (e.ConsultationEndTime!.Value - e.ConsultationStartTime!.Value).TotalMinutes)
            .ToList();

        var waitingDtos = waiting.Select(e => MapQueueItem(e, e.Appointment!)).ToList();

        return new AdminQueueDashboardDto(
            centerId,
            center.CenterName,
            date,
            entries.Count,
            entries.Count(e => e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Called),
            entries.Count(e => e.Status == QueueStatus.InConsultation),
            entries.Count(e => e.Status == QueueStatus.Completed),
            entries.Count(e => e.Status == QueueStatus.Missed),
            completedDurations.Count == 0 ? null : completedDurations.Average(),
            inConsult is null || inConsult.Appointment is null ? null : MapQueueItem(inConsult, inConsult.Appointment),
            waitingDtos);
    }

    private static QueueItemDto MapQueueItem(QueueEntry queue, Appointment appointment)
    {
        return new QueueItemDto(
            queue.Id,
            queue.AppointmentId,
            queue.QueueNumber,
            queue.Position,
            queue.Status.ToString(),
            queue.EstimatedWaitTimeMinutes,
            MaskName(appointment.Patient?.FullName),
            appointment.Doctor?.FullName ?? string.Empty,
            appointment.AppointmentTime,
            queue.CalledTime,
            queue.ConsultationStartTime,
            queue.RoomNumber);
    }

    private static string MaskName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return "Patient";

        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
            return parts[0];

        return $"{parts[0]} {parts[1][0]}.";
    }

    private static string BuildStatusMessage(QueueEntry queue) => queue.Status switch
    {
        QueueStatus.Waiting => $"You are #{queue.Position} in queue, estimated wait: {FormatWaitTime(queue.EstimatedWaitTimeMinutes)}",
        QueueStatus.Called => "You've been called. Please proceed to your consultation room.",
        QueueStatus.InConsultation => "Your consultation is in progress",
        QueueStatus.Completed => "Your consultation is complete",
        QueueStatus.Missed => "You were marked as missed",
        QueueStatus.Cancelled => "Queue entry cancelled",
        _ => "Queue status updated"
    };

    public static string FormatWaitTime(int totalMinutes)
    {
        if (totalMinutes <= 0) return "0m";
        var h = totalMinutes / 60;
        var m = totalMinutes % 60;
        return h > 0 && m > 0 ? $"{h}h {m}m" : h > 0 ? $"{h}h" : $"{m}m";
    }
}
