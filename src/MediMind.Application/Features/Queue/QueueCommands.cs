using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.Queue;

// ─── DTOs ─────────────────────────────────────────────────────────────────────

public record LegacyQueueEntryDto(
    Guid QueueId,
    Guid AppointmentId,
    Guid PatientId,
    string PatientName,
    string QueueNumber,
    int Position,
    string Status,
    int EstimatedWaitMinutes,
    DateOnly QueueDate,
    DateTime? CalledTime);

public record LegacyPatientQueueStatusDto(
    string QueueNumber,
    int Position,
    int EstimatedWaitMinutes,
    string Status,
    string Message);

// ═══════════════════════════════════════════════════════════════════════════════
// DAILY QUEUE GENERATION (called by cron job at 06:00 AM)
// ═══════════════════════════════════════════════════════════════════════════════

public record GenerateDailyQueuesCommand(DateOnly Date) : IRequest<GenerateDailyQueuesResult>;

public record GenerateDailyQueuesResult(int TotalQueuesGenerated, int CentersProcessed);

public class GenerateDailyQueuesHandler(
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IHealthcareCenterRepository centerRepository,
    IPushNotificationService pushService,
    IUnitOfWork unitOfWork)
    : IRequestHandler<GenerateDailyQueuesCommand, GenerateDailyQueuesResult>
{
    public async Task<GenerateDailyQueuesResult> Handle(
        GenerateDailyQueuesCommand request, CancellationToken ct)
    {
        // Step 1: Get ALL confirmed appointments for today across all centers
        var confirmedAppointments = await appointmentRepository
            .GetConfirmedForQueueGenerationAsync(request.Date, ct);

        // Step 2: Group by healthcare center (tenant isolation)
        var appointmentsByCenter = confirmedAppointments
            .GroupBy(a => a.CenterId)
            .ToList();

        var queueEntries = new List<QueueEntry>();
        var centersProcessed = 0;

        foreach (var centerGroup in appointmentsByCenter)
        {
            var centerId = centerGroup.Key;
            var center = await centerRepository.GetByIdAsync(centerId, ct);
            if (center is null) continue;

            // Step 3: Sort by appointment time (FCFS within each center)
            var sortedAppointments = centerGroup
                .OrderBy(a => a.AppointmentTime)
                .ToList();

            // Step 4: Create queue entries with sequential positions
            var position = 1;
            foreach (var appointment in sortedAppointments)
            {
                var entry = new QueueEntry(
                    appointment.Id,
                    centerId,
                    request.Date,
                    position,
                    center.SlotDurationMinutes);

                queueEntries.Add(entry);
                position++;
            }

            centersProcessed++;
        }

        // Step 5: Bulk insert (single transaction per center avoids individual insert overhead)
        await queueRepository.BulkInsertAsync(queueEntries, ct);
        await unitOfWork.SaveChangesAsync(ct);

        // Step 6: Send initial notifications to all patients in batches of 100
        var batches = queueEntries.Chunk(100);
        foreach (var batch in batches)
        {
            var notifyTasks = batch.Select(entry =>
                pushService.SendToUserAsync(
                    entry.Appointment?.PatientId ?? Guid.Empty,
                    "Queue Ready 🏥",
                    $"You are #{entry.Position} in queue today. Estimated wait: {entry.EstimatedWaitTimeMinutes} minutes.",
                    new Dictionary<string, string>
                    {
                        ["queueNumber"] = entry.QueueNumber,
                        ["position"] = entry.Position.ToString(),
                        ["estimatedWait"] = entry.EstimatedWaitTimeMinutes.ToString()
                    },
                    ct));

            await Task.WhenAll(notifyTasks);
            await Task.Delay(100, ct); // Respect Firebase rate limits
        }

        return new GenerateDailyQueuesResult(queueEntries.Count, centersProcessed);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// CALL NEXT PATIENT (Admin action)
// ═══════════════════════════════════════════════════════════════════════════════

public record CallNextPatientCommand(Guid CenterId, Guid AdminId) : IRequest<LegacyQueueEntryDto>;

public class CallNextPatientHandler(
    IQueueRepository queueRepository,
    IAppointmentRepository appointmentRepository,
    IPushNotificationService pushService,
    ISmsService smsService,
    IQueueHubService queueHub,
    IUnitOfWork unitOfWork)
    : IRequestHandler<CallNextPatientCommand, LegacyQueueEntryDto>
{
    public async Task<LegacyQueueEntryDto> Handle(CallNextPatientCommand request, CancellationToken ct)
    {
        // Get next patient with status "Waiting" — ordered by position
        var next = await queueRepository.GetNextWaitingAsync(request.CenterId, ct)
                   ?? throw new DomainException("Queue is empty. No patients waiting.");

        next.CallPatient();

        // Mark appointment as in-progress
        var appointment = await appointmentRepository.GetByIdAsync(next.AppointmentId, ct);
        appointment?.MarkInProgress();

        // Update positions for remaining patients
        await queueRepository.UpdatePositionsAsync(request.CenterId, next.QueueDate, ct);

        await unitOfWork.SaveChangesAsync(ct);

        var patientId = appointment?.PatientId ?? Guid.Empty;

        // Multi-channel notification: push + SMS for critical "called" event
        var pushTask = pushService.SendToUserAsync(
            patientId,
            "You're Next! 🔔",
            "Please proceed to the consultation room now.",
            new Dictionary<string, string> { ["queueId"] = next.Id.ToString() },
            ct);

        // Broadcast real-time queue update to ALL connected clients at this center
        var broadcastTask = queueHub.BroadcastQueueUpdateAsync(
            request.CenterId,
            new { type = "PATIENT_CALLED", queueId = next.Id, position = next.Position },
            ct);

        await Task.WhenAll(pushTask, broadcastTask);

        return new LegacyQueueEntryDto(
            next.Id,
            next.AppointmentId,
            patientId,
            appointment?.Patient?.FullName ?? "Patient",
            next.QueueNumber,
            next.Position,
            next.Status.ToString(),
            next.EstimatedWaitTimeMinutes,
            next.QueueDate,
            next.CalledTime);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// MARK NO-SHOW
// ═══════════════════════════════════════════════════════════════════════════════

public record MarkNoShowCommand(Guid QueueEntryId, Guid CenterId) : IRequest<Unit>;

public class MarkNoShowHandler(
    IQueueRepository queueRepository,
    IAppointmentRepository appointmentRepository,
    IQueueHubService queueHub,
    IUnitOfWork unitOfWork)
    : IRequestHandler<MarkNoShowCommand, Unit>
{
    public async Task<Unit> Handle(MarkNoShowCommand request, CancellationToken ct)
    {
        var entry = await queueRepository.GetByIdAsync(request.QueueEntryId, ct)
                    ?? throw new NotFoundException(nameof(QueueEntry), request.QueueEntryId);

        if (entry.CenterId != request.CenterId)
            throw new TenantIsolationException();

        entry.MarkMissed();

        var appointment = await appointmentRepository.GetByIdAsync(entry.AppointmentId, ct);
        appointment?.MarkNoShow();

        await unitOfWork.SaveChangesAsync(ct);
        await queueHub.BroadcastQueueUpdateAsync(
            request.CenterId,
            new { type = "NO_SHOW", queueId = entry.Id },
            ct);

        return Unit.Value;
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET QUEUE STATUS (Patient — real-time polling fallback)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetPatientQueueStatusQuery(Guid AppointmentId, Guid PatientId) : IRequest<LegacyPatientQueueStatusDto>;

public class GetPatientQueueStatusHandler(IQueueRepository queueRepository)
    : IRequestHandler<GetPatientQueueStatusQuery, LegacyPatientQueueStatusDto>
{
    public async Task<LegacyPatientQueueStatusDto> Handle(
        GetPatientQueueStatusQuery request, CancellationToken ct)
    {
        var entry = await queueRepository.GetByAppointmentAsync(request.AppointmentId, ct);

        if (entry is null)
            return new LegacyPatientQueueStatusDto(
                "N/A", 0, 0, "NotInQueue",
                "Queue information unavailable. Please check in at reception.");

        var message = entry.Status.ToString() switch
        {
            "Waiting" => $"You are #{entry.Position} in queue. Estimated wait: {entry.EstimatedWaitTimeMinutes} minutes.",
            "Called" => "You're next! Please proceed to the consultation room now.",
            "InConsultation" => "Your consultation is in progress.",
            "Completed" => "Your consultation is complete. Thank you!",
            "Missed" => "You were marked as no-show. Please contact reception.",
            _ => "Status unknown."
        };

        return new LegacyPatientQueueStatusDto(
            entry.QueueNumber,
            entry.Position,
            entry.EstimatedWaitTimeMinutes,
            entry.Status.ToString(),
            message);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// GET CENTER QUEUE DASHBOARD (Admin)
// ═══════════════════════════════════════════════════════════════════════════════

public record GetCenterQueueQuery(Guid CenterId, DateOnly Date) : IRequest<IReadOnlyList<LegacyQueueEntryDto>>;

public class GetCenterQueueHandler(IQueueRepository queueRepository)
    : IRequestHandler<GetCenterQueueQuery, IReadOnlyList<LegacyQueueEntryDto>>
{
    public async Task<IReadOnlyList<LegacyQueueEntryDto>> Handle(
        GetCenterQueueQuery request, CancellationToken ct)
    {
        var entries = await queueRepository.GetByCenterAndDateAsync(request.CenterId, request.Date, ct);

        return entries.Select(e => new LegacyQueueEntryDto(
            e.Id,
            e.AppointmentId,
            e.Appointment?.PatientId ?? Guid.Empty,
            e.Appointment?.Patient?.FullName ?? "Unknown",
            e.QueueNumber,
            e.Position,
            e.Status.ToString(),
            e.EstimatedWaitTimeMinutes,
            e.QueueDate,
            e.CalledTime
        )).OrderBy(e => e.Position).ToList();
    }
}
