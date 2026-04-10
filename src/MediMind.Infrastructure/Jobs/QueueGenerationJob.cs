using MediatR;
using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Jobs;

/// <summary>
/// Adapter that allows the application queue generation handler to be used as a Hangfire recurring job.
/// </summary>
public class QueueGenerationJob : IQueueGenerationJob
{
    private readonly IMediator _mediator;
    private readonly ILogger<QueueGenerationJob> _logger;

    public QueueGenerationJob(IMediator mediator, ILogger<QueueGenerationJob> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task GenerateDailyQueuesAsync(DateOnly date, CancellationToken ct = default)
    {
        _logger.LogInformation("QueueGenerationJob triggered for date {Date}", date);
        var result = await _mediator.Send(new GenerateDailyQueuesCommand(date), ct);
        _logger.LogInformation("Generated {Total} queues for {Centers} centers.", result.TotalQueuesGenerated, result.CentersProcessed);
    }
}

