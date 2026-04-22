using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Infrastructure.SignalR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

public class MediMindNotificationService(
    FcmClient fcmClient,
    GeezSmsClient geezSmsClient,
    IUserDeviceTokenRepository deviceTokenRepository,
    INotificationLogRepository notificationLogRepository,
    IUnitOfWork unitOfWork,
    IHubContext<QueueHub> hubContext,
    ILogger<MediMindNotificationService> logger) : INotificationService
{
    public async Task SendPushAsync(
        Guid userId,
        string title,
        string body,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        var tokens = await deviceTokenRepository.GetActiveTokensForUserAsync(userId, ct);
        var tokenStrings = tokens.Select(t => t.FcmToken).ToList();
        if (tokenStrings.Count == 0)
        {
            logger.LogWarning("No active FCM tokens for user {UserId}; push skipped.", userId);
            await LogAsync(
                userId,
                null,
                "Push",
                "Push",
                title,
                body,
                "Failed",
                null,
                "No registered device tokens.",
                ct);
            return;
        }

        var payload = new NotificationPayload(title, body, data, null, "high", null);
        try
        {
            var ids = await fcmClient.SendToMultipleTokensAsync(userId, tokenStrings, payload, ct);
            var external = ids.Count > 0 ? string.Join(',', ids.Take(3)) : null;
            await LogAsync(
                userId,
                null,
                "Push",
                "Push",
                title,
                body,
                ids.Count > 0 ? "Sent" : "Failed",
                external,
                ids.Count > 0 ? null : "FCM returned no successful sends.",
                ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Push send failed for user {UserId}", userId);
            await LogAsync(userId, null, "Push", "Push", title, body, "Failed", null, ex.Message, ct);
        }
    }

    public async Task SendSmsAsync(string phoneNumber, string message, CancellationToken ct = default)
    {
        try
        {
            var response = await geezSmsClient.SendSmsAsync(phoneNumber, message, ct);
            var ok = response is not null && IsGeezSuccess(response);
            await LogAsync(
                null,
                EthiopiaPhone.Normalize(phoneNumber),
                "Sms",
                "SMS",
                null,
                message,
                ok ? "Sent" : "Failed",
                null,
                ok ? null : response?.Message,
                ct);

            if (!ok)
                throw new InvalidOperationException(
                    $"SMS send rejected: {response?.Message ?? "unknown"}");
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await LogAsync(
                null,
                phoneNumber,
                "Sms",
                "SMS",
                null,
                message,
                "Failed",
                null,
                ex.Message,
                ct);
            throw;
        }
    }

    public async Task SendBothAsync(
        Guid userId,
        string phoneNumber,
        string title,
        string body,
        string smsMessage,
        Dictionary<string, string>? data = null,
        CancellationToken ct = default)
    {
        await SendPushAsync(userId, title, body, data, ct);
        try
        {
            await SendSmsAsync(phoneNumber, smsMessage, ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SMS leg of SendBothAsync failed for user {UserId}", userId);
        }
    }

    public async Task SendToMultipleUsersAsync(
        IEnumerable<Guid> userIds,
        string title,
        string body,
        CancellationToken ct = default)
    {
        foreach (var userId in userIds.Distinct())
            await SendPushAsync(userId, title, body, null, ct);
    }

    public async Task RegisterDeviceTokenAsync(
        Guid userId,
        string fcmToken,
        string platform,
        string? deviceModel,
        CancellationToken ct = default)
    {
        var entity = new UserDeviceToken(userId, fcmToken, platform, deviceModel);
        await deviceTokenRepository.UpsertAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task DeregisterDeviceTokenAsync(Guid userId, string fcmToken, CancellationToken ct = default)
    {
        await deviceTokenRepository.DeactivateAsync(userId, fcmToken, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task SendQueueUpdateToPatientAsync(Guid patientId, object status)
    {
        await hubContext.Clients
            .Group($"patient_{patientId}")
            .SendAsync("QueuePositionUpdated", status);
    }

    public async Task BroadcastQueueRefreshAsync(Guid centerId, object dashboard)
    {
        await hubContext.Clients
            .Group($"center_{centerId}")
            .SendAsync("QueueRefreshed", dashboard);
    }

    public async Task BroadcastQueueEventAsync(Guid centerId, string eventName, object payload)
    {
        await hubContext.Clients
            .Group($"center_{centerId}")
            .SendAsync(eventName, payload);
    }

    private async Task LogAsync(
        Guid? userId,
        string? phone,
        string notificationType,
        string channel,
        string? title,
        string body,
        string status,
        string? externalRef,
        string? error,
        CancellationToken ct)
    {
        var log = new NotificationLog(
            userId,
            phone,
            notificationType,
            channel,
            title,
            body,
            status,
            externalRef,
            error,
            DateTime.UtcNow);
        await notificationLogRepository.AddAsync(log, ct);
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static bool IsGeezSuccess(GeezSendResponse r) =>
        r.Status == "success";
}
