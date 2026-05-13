using System.Collections.Generic;
using Hangfire;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

/// <summary>
/// Background service that sends appointment reminders every five minutes.
/// Respects per-patient notification preferences (GAP-3).
/// SMS failures are retried once after 30 minutes via Hangfire (GAP-8).
/// </summary>
public class AppointmentReminderService(
    IServiceScopeFactory scopeFactory,
    ILogger<AppointmentReminderService> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessReminderWindow(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Appointment reminder cycle failed");
            }

            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    private async Task ProcessReminderWindow(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var appointmentRepository = scope.ServiceProvider.GetRequiredService<IAppointmentRepository>();
        var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
        var prefRepository = scope.ServiceProvider.GetRequiredService<INotificationPreferenceRepository>();
        var dbContext = scope.ServiceProvider.GetRequiredService<Data.MediMindDbContext>();

        var now = DateTime.UtcNow;

        var reminders24h = await appointmentRepository.GetUpcomingForReminderAsync(now.AddHours(24), ReminderType.TwentyFourHours);
        foreach (var appointment in reminders24h)
        {
            var prefs = await prefRepository.GetByUserIdAsync(appointment.PatientId, ct);
            var message = $"MediMind: Appointment with Dr. {appointment.Doctor.FullName} on {appointment.AppointmentDate:MMM dd} at {appointment.AppointmentTime:HH\\:mm} at {appointment.Center.CenterName}, {appointment.Center.Address}. Reply CONFIRM or CANCEL.";

            if (prefs is null || prefs.AppointmentRemindersSms)
            {
                try
                {
                    await notificationService.SendSmsAsync(appointment.Patient.PhoneNumber, message, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "24h SMS reminder failed for appointment {AppointmentId}; scheduling retry in 30 min", appointment.Id);
                    BackgroundJob.Schedule<IAppointmentSmsRetryJob>(
                        j => j.RetrySmsAsync(appointment.Id, appointment.Patient.PhoneNumber, message, CancellationToken.None),
                        TimeSpan.FromMinutes(30));
                }
            }

            if (prefs is null || prefs.AppointmentRemindersPush)
            {
                await notificationService.SendPushAsync(
                    appointment.PatientId,
                    "Appointment Tomorrow",
                    $"Dr. {appointment.Doctor.FullName} tomorrow at {appointment.AppointmentTime:HH\\:mm}. Tap to confirm or reschedule.",
                    new Dictionary<string, string>
                    {
                        ["appointmentId"] = appointment.Id.ToString(),
                        ["action"] = "appointment_reminder"
                    },
                    ct);
            }

            appointment.MarkReminderSent(ReminderType.TwentyFourHours);
        }

        var reminders2h = await appointmentRepository.GetUpcomingForReminderAsync(now.AddHours(2), ReminderType.TwoHours);
        foreach (var appointment in reminders2h)
        {
            var prefs = await prefRepository.GetByUserIdAsync(appointment.PatientId, ct);
            if (prefs is not null && !prefs.AppointmentRemindersPush)
            {
                appointment.MarkReminderSent(ReminderType.TwoHours);
                continue;
            }

            var body = $"Dr. {appointment.Doctor.FullName} in about 2 hours at {appointment.Center.CenterName}. Tap to confirm or reschedule.";
            await notificationService.SendPushAsync(
                appointment.PatientId,
                "Appointment in 2 hours",
                body,
                new Dictionary<string, string>
                {
                    ["appointmentId"] = appointment.Id.ToString(),
                    ["action"] = "appointment_reminder"
                },
                ct);
            appointment.MarkReminderSent(ReminderType.TwoHours);
        }

        await dbContext.SaveChangesAsync(ct);
    }
}
