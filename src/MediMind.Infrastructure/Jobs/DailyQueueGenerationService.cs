using MediMind.Application.Features.Queue;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Jobs;

/// <summary>
/// Generates queues daily at 06:00 AM Ethiopia time.
/// </summary>
public class DailyQueueGenerationService(
    IServiceScopeFactory scopeFactory,
    ILogger<DailyQueueGenerationService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var tz = ResolveEthiopiaTimeZone();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
            var nextRunLocal = nowLocal.TimeOfDay < TimeSpan.FromHours(6)
                ? nowLocal.Date.AddHours(6)
                : nowLocal.Date.AddDays(1).AddHours(6);

            var nextRunUtc = TimeZoneInfo.ConvertTimeToUtc(nextRunLocal, tz);
            var delay = nextRunUtc - DateTime.UtcNow;
            if (delay < TimeSpan.Zero)
                delay = TimeSpan.FromSeconds(1);

            logger.LogInformation("Daily queue generation scheduled for {NextRunLocal} ({NextRunUtc} UTC)", nextRunLocal, nextRunUtc);

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<IQueueService>();
                await queueService.GenerateQueuesForAllCentersAsync(DateOnly.FromDateTime(DateTime.UtcNow));
                logger.LogInformation("Daily queue generation completed.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Daily queue generation failed.");
            }
        }
    }

    private static TimeZoneInfo ResolveEthiopiaTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Africa/Addis_Ababa");
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById("E. Africa Standard Time");
        }
    }
}
