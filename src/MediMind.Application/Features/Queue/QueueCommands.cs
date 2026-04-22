using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.Queue;

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
