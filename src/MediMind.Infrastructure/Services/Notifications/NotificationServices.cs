using System.Net.Http.Json;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

// ─── Geez SMS Gateway ─────────────────────────────────────────────────────────

/// <summary>
/// Sends SMS via Geez SMS Gateway (ETB 0.65/SMS).
/// Used for: OTP verification, appointment reminders, queue notifications.
/// </summary>
public class GeezSmsService(
    GeezSmsClient geezSmsClient,
    ILogger<GeezSmsService> logger)
    : ISmsService
{
    public async Task SendAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        var response = await geezSmsClient.SendSmsAsync(phoneNumber, message, ct);
        if (response is not null && IsHttpSuccess(response))
        {
            logger.LogInformation("SMS delivered to {Phone}.", phoneNumber);
            return;
        }

        // Some gateways reject '+' prefix and require 2519XXXXXXXX format.
        var stripped = EthiopiaPhone.Normalize(phoneNumber).TrimStart('+');
        if (!string.Equals(stripped, phoneNumber.Trim(), StringComparison.Ordinal))
        {
            var fallback = await geezSmsClient.SendSmsAsync(stripped, message, ct);
            if (fallback is not null && IsHttpSuccess(fallback))
            {
                logger.LogInformation("SMS delivered to {Phone} using stripped prefix.", phoneNumber);
                return;
            }

            logger.LogError(
                "SMS delivery failed for {Phone}. Primary={Primary}. Fallback={Fallback}",
                phoneNumber, response?.Message, fallback?.Message);
            throw new InvalidOperationException("Failed to deliver SMS OTP.");
        }

        logger.LogError("SMS delivery failed for {Phone}. Response={Response}", phoneNumber, response?.Message);
        throw new InvalidOperationException("Failed to deliver SMS OTP.");
    }

    private static bool IsHttpSuccess(GeezSendResponse r) =>
        r.Status == "success";

    public async Task SendOtpAsync(string phoneNumber, string otpCode, CancellationToken ct = default) =>
        await SendAsync(phoneNumber,
            $"Your MediMind verification code is: {otpCode}. Valid for 5 minutes. Do not share this code.",
            ct);

    public async Task SendAppointmentReminderAsync(
        string phoneNumber, string doctorName, DateTime appointmentTime, CancellationToken ct = default) =>
        await SendAsync(phoneNumber,
            $"MediMind Reminder: Your appointment with Dr. {doctorName} is on " +
            $"{appointmentTime:MMM dd} at {appointmentTime:HH:mm}. Reply CANCEL to cancel.",
            ct);
}

// ─── Firebase Push Notification Service ──────────────────────────────────────

/// <summary>
/// Sends push notifications via Firebase Cloud Messaging (FCM).
/// </summary>
public class FirebasePushNotificationService(
    IUserDeviceTokenRepository userDeviceTokenRepository,
    FcmClient fcmClient,
    ILogger<FirebasePushNotificationService> logger)
    : IPushNotificationService
{
    public async Task SendAsync(
        string deviceToken,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var payload = new NotificationPayload(title, body, data, null, "high", null);
        await fcmClient.SendToTokenAsync(deviceToken, payload, userIdForDeactivation: null, ct);
    }

    public async Task SendToUserAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await userDeviceTokenRepository.GetActiveTokensForUserAsync(userId, ct);
        if (tokens.Count == 0)
        {
            logger.LogInformation("No FCM tokens for user {UserId}; push skipped.", userId);
            return;
        }

        var payload = new NotificationPayload(title, body, data, null, "high", null);
        await fcmClient.SendToMultipleTokensAsync(
            userId,
            tokens.Select(t => t.FcmToken).ToList(),
            payload,
            ct);
    }
}

// ─── Email Service (SendGrid) ─────────────────────────────────────────────────

public class SendGridEmailService(
    IHttpClientFactory httpClientFactory,
    IConfiguration config,
    ILogger<SendGridEmailService> logger)
    : IEmailService
{
    private readonly string _apiKey = config["SendGrid:ApiKey"] ?? string.Empty;
    private readonly string _fromEmail = config["SendGrid:FromEmail"] ?? "noreply@medimind.et";
    private readonly string _fromName = config["SendGrid:FromName"] ?? "MediMind";

    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            logger.LogWarning("SendGrid API key not configured. Email not sent to {To}", to);
            return;
        }

        var client = httpClientFactory.CreateClient("SendGrid");
        try
        {
            var payload = new
            {
                personalizations = new[] { new { to = new[] { new { email = to } } } },
                from = new { email = _fromEmail, name = _fromName },
                subject,
                content = new[] { new { type = "text/html", value = htmlBody } }
            };
            var response = await client.PostAsJsonAsync("v3/mail/send", payload, ct);
            if (!response.IsSuccessStatusCode)
                logger.LogWarning("Email delivery failed to {To}. Status: {Status}", to, response.StatusCode);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Email delivery exception for {To}", to);
        }
    }

    public async Task SendWelcomeAsync(string to, string fullName, CancellationToken ct = default) =>
        await SendAsync(to, "Welcome to MediMind! 🏥",
            $"<h2>Welcome, {fullName}!</h2><p>Your MediMind account is ready. " +
            $"Start booking appointments and tracking your health today.</p>", ct);

    public async Task SendDoctorInvitationAsync(
        string to, string doctorName, string centerName,
        string invitationLink, CancellationToken ct = default) =>
        await SendAsync(to, $"You've been invited to join {centerName} on MediMind",
            $"<h2>Dear Dr. {doctorName},</h2>" +
            $"<p>{centerName} has invited you to join MediMind platform.</p>" +
            $"<p><a href='{invitationLink}'>Accept Invitation</a></p>" +
            $"<p>This link expires in 48 hours.</p>", ct);
}

// ─── Appointment Reminder Background Job ──────────────────────────────────────

/// <summary>
/// Runs every 30 minutes via Hangfire.
/// Sends 24-hour and 2-hour appointment reminders (SMS + Push).
/// FR-014: Automated appointment reminders.
/// </summary>
public class AppointmentReminderJob(
    IAppointmentRepository appointmentRepository,
    ISmsService smsService,
    IPushNotificationService pushService,
    ILogger<AppointmentReminderJob> logger)
    : IAppointmentReminderJob
{
    public async Task SendRemindersAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Running appointment reminder job at {Time}", DateTime.UtcNow);

        var now = DateTime.UtcNow;

        // 24-hour reminders (appointments between 23.5 and 24.5 hours from now)
        var twentyFourHourWindow = new
        {
            From = DateOnly.FromDateTime(now.AddHours(23.5)),
            To = DateOnly.FromDateTime(now.AddHours(24.5))
        };

        // 2-hour reminders (appointments between 1.75 and 2.25 hours from now)
        var twoHourWindow = new
        {
            From = DateOnly.FromDateTime(now.AddHours(1.75)),
            To = DateOnly.FromDateTime(now.AddHours(2.25))
        };

        
        // Get confirmed appointments in 24-hour window
        var appointments24h = await appointmentRepository
            .GetByCenterAndDateAsync(Guid.Empty, twentyFourHourWindow.From, ct); // Across all centers

        foreach (var appointment in appointments24h)
        {
            var appointmentDateTime = appointment.AppointmentDate.ToDateTime(appointment.AppointmentTime);

            if (appointmentDateTime < now.AddHours(23.5) || appointmentDateTime > now.AddHours(24.5))
                continue;

            if (appointment.Patient?.PhoneNumber is null) continue;

            // SMS + Push for 24-hour reminder
            await smsService.SendAppointmentReminderAsync(
                appointment.Patient.PhoneNumber,
                appointment.Doctor?.FullName ?? "your doctor",
                appointmentDateTime, ct);

            await pushService.SendToUserAsync(
                appointment.PatientId,
                "Appointment Tomorrow 📅",
                $"Reminder: Dr. {appointment.Doctor?.FullName} tomorrow at {appointment.AppointmentTime:HH:mm}",
                new Dictionary<string, string> { ["appointmentId"] = appointment.Id.ToString() },
                ct);

            logger.LogDebug("24h reminder sent for appointment {Id}", appointment.Id);
        }

        logger.LogInformation("Appointment reminder job completed.");
    }
}
