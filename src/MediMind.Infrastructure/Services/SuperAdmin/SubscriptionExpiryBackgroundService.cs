using MediMind.Application.Features.SuperAdmin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.SuperAdmin;

/// <summary>
/// Runs daily at midnight UTC to expire subscriptions past their end date.
/// </summary>
public sealed class SubscriptionExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionExpiryBackgroundService> logger) : IHostedService, IDisposable
{
    private Timer? _timer;

    public Task StartAsync(CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var nextMidnight = now.Date.AddDays(1);
        var initialDelay = nextMidnight - now;

        _timer = new Timer(DoWork, null, initialDelay, TimeSpan.FromDays(1));
        logger.LogInformation("SubscriptionExpiryBackgroundService scheduled. First run at {NextMidnight:O}", nextMidnight);
        return Task.CompletedTask;
    }

    private void DoWork(object? _)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ISuperAdminSubscriptionService>();
            service.ApplyExpiredSubscriptionsAsync(CancellationToken.None).GetAwaiter().GetResult();
            logger.LogInformation("Subscription expiry check completed at {Time:O}", DateTime.UtcNow);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Subscription expiry background job failed.");
        }
    }

    public Task StopAsync(CancellationToken ct)
    {
        _timer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose() => _timer?.Dispose();
}
