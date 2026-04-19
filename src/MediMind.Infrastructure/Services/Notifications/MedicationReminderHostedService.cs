using System.Text.Json;
using MediMind.Application.Notifications;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

/// <summary>
/// Sends medication reminders once per scheduled minute per reminder (deduplicated via memory cache).
/// </summary>
public class MedicationReminderHostedService(
    IServiceScopeFactory scopeFactory,
    IMemoryCache cache,
    ILogger<MedicationReminderHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TickAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Medication reminder tick failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task TickAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var reminders = await scope.ServiceProvider
            .GetRequiredService<IMedicationReminderRepository>()
            .GetAllActiveAsync(ct);

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var nowTime = TimeOnly.FromDateTime(now);

        foreach (var reminder in reminders)
        {
            if (!ShouldRunFrequency(reminder, now))
                continue;

            List<TimeOnly> times;
            try
            {
                times = ParseReminderTimes(reminder.ReminderTimes);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Invalid ReminderTimes JSON for reminder {ReminderId}", reminder.Id);
                continue;
            }

            foreach (var t in times)
            {
                if (!IsWithinOneMinute(nowTime, t))
                    continue;

                var dedupeKey = $"reminder_sent_{reminder.Id}_{today:yyyy-MM-dd}_{t:HH-mm}";
                if (cache.TryGetValue(dedupeKey, out _))
                    continue;

                cache.Set(dedupeKey, true, new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(2)
                });

                var tpl = NotificationTemplates.MedicationReminder(reminder.MedicationName, reminder.Dosage);
                var notification = scope.ServiceProvider.GetRequiredService<INotificationService>();
                await notification.SendPushAsync(
                    reminder.PatientId,
                    tpl.Title,
                    tpl.Body,
                    new Dictionary<string, string>
                    {
                        ["reminderId"] = reminder.Id.ToString(),
                        ["type"] = "medication_reminder"
                    },
                    ct);
            }
        }
    }

    private static bool ShouldRunFrequency(MedicationReminder reminder, DateTime nowUtc)
    {
        return reminder.Frequency switch
        {
            "Daily" => true,
            "Weekly" => nowUtc.DayOfWeek == reminder.CreatedAt.DayOfWeek,
            "Custom" => true,
            _ => true
        };
    }

    private static List<TimeOnly> ParseReminderTimes(string json)
    {
        var strings = JsonSerializer.Deserialize<List<string>>(json) ?? [];
        var result = new List<TimeOnly>();
        foreach (var s in strings)
        {
            if (TimeOnly.TryParse(s, out var time))
                result.Add(time);
        }

        return result;
    }

    private static bool IsWithinOneMinute(TimeOnly now, TimeOnly target)
    {
        var delta = Math.Abs((now - target).TotalMinutes);
        return delta <= 1.0;
    }
}
