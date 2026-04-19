using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using MediMind.Domain.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Notifications;

public class FcmClient(
    IUserDeviceTokenRepository deviceTokenRepository,
    ILogger<FcmClient> logger)
{
    public async Task<string> SendToTokenAsync(
        string fcmToken,
        NotificationPayload payload,
        Guid? userIdForDeactivation,
        CancellationToken ct = default)
    {
        EnsureFirebaseReady();
        var message = BuildMessage(fcmToken, payload, topic: null);
        try
        {
            return await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
        }
        catch (FirebaseMessagingException ex)
        {
            await HandleMessagingExceptionAsync(ex, userIdForDeactivation, fcmToken, ct);
            throw;
        }
    }

    public async Task<string> SendToTopicAsync(string topic, NotificationPayload payload, CancellationToken ct = default)
    {
        EnsureFirebaseReady();
        var message = BuildMessage(token: null, payload, topic);
        return await FirebaseMessaging.DefaultInstance.SendAsync(message, ct);
    }

    /// <summary>
    /// Sends FCM multicast (max 500 tokens per batch). Deactivates invalid tokens for the given user.
    /// </summary>
    public async Task<IReadOnlyList<string>> SendToMultipleTokensAsync(
        Guid userId,
        IReadOnlyList<string> tokens,
        NotificationPayload payload,
        CancellationToken ct = default)
    {
        if (tokens.Count == 0)
            return Array.Empty<string>();

        EnsureFirebaseReady();

        var messageIds = new List<string>();
        foreach (var chunk in tokens.Chunk(500))
        {
            var chunkList = chunk.ToList();
            var multicast = new MulticastMessage
            {
                Tokens = chunkList,
                Notification = new FirebaseAdmin.Messaging.Notification
                {
                    Title = payload.Title,
                    Body = payload.Body
                },
                Data = payload.Data?.ToDictionary(kv => kv.Key, kv => kv.Value) ?? new Dictionary<string, string>(),
                Android = BuildAndroid(payload),
                Apns = BuildApns(payload)
            };

            var response = await FirebaseMessaging.DefaultInstance.SendEachForMulticastAsync(multicast, ct);
            messageIds.AddRange(response.Responses
                .Where(r => r.Exception is null && !string.IsNullOrEmpty(r.MessageId))
                .Select(r => r.MessageId!));

            for (var i = 0; i < response.Responses.Count; i++)
            {
                var r = response.Responses[i];
                if (r.Exception is null)
                    continue;

                var token = chunkList[i];
                var ex = r.Exception;
                if (ex is FirebaseMessagingException fme)
                    await HandleMessagingExceptionAsync(fme, userId, token, ct);
                else
                    logger.LogWarning(ex, "FCM send failed for token ending ...{Suffix}", token[^Math.Min(8, token.Length)..]);
            }
        }

        return messageIds;
    }

    private static Message BuildMessage(string? token, NotificationPayload payload, string? topic)
    {
        var message = new Message
        {
            Notification = new FirebaseAdmin.Messaging.Notification
            {
                Title = payload.Title,
                Body = payload.Body
            },
            Android = BuildAndroid(payload),
            Apns = BuildApns(payload)
        };

        if (payload.Data is not null)
        {
            message.Data = new Dictionary<string, string>(payload.Data);
        }

        if (!string.IsNullOrEmpty(topic))
            message.Topic = topic;
        else
            message.Token = token!;

        return message;
    }

    private static AndroidConfig BuildAndroid(NotificationPayload payload)
    {
        var priority = string.Equals(payload.Priority, "normal", StringComparison.OrdinalIgnoreCase)
            ? Priority.Normal
            : Priority.High;

        var notification = new AndroidNotification();
        if (!string.IsNullOrWhiteSpace(payload.ClickAction))
            notification.ClickAction = payload.ClickAction;
        if (!string.IsNullOrWhiteSpace(payload.ImageUrl))
            notification.ImageUrl = payload.ImageUrl;

        return new AndroidConfig
        {
            Priority = priority,
            Notification = notification
        };
    }

    private static ApnsConfig BuildApns(NotificationPayload payload)
    {
        var headers = new Dictionary<string, string>
        {
            ["apns-priority"] = string.Equals(payload.Priority, "normal", StringComparison.OrdinalIgnoreCase) ? "5" : "10"
        };

        return new ApnsConfig { Headers = headers };
    }

    private void EnsureFirebaseReady()
    {
        if (FirebaseApp.DefaultInstance is null)
            throw new InvalidOperationException("Firebase is not initialized. Set Firebase:ServiceAccountJson (or ServiceAccountPath) in configuration.");
    }

    private async Task HandleMessagingExceptionAsync(
        FirebaseMessagingException ex,
        Guid? userId,
        string fcmToken,
        CancellationToken ct)
    {
        var code = ex.MessagingErrorCode?.ToString() ?? string.Empty;
        var isUnregistered =
            string.Equals(code, "Unregistered", StringComparison.OrdinalIgnoreCase)
            || ex.Message.Contains("registration-token-not-registered", StringComparison.OrdinalIgnoreCase);

        if (isUnregistered)
        {
            if (userId.HasValue)
                await deviceTokenRepository.DeactivateAsync(userId.Value, fcmToken, ct);
            else
                await deviceTokenRepository.DeactivateTokenStringAsync(fcmToken, ct);

            logger.LogInformation("Deactivated invalid FCM token for user {UserId}", userId);
        }
        else
        {
            logger.LogWarning(ex, "FCM error {Code} for token ending ...{Suffix}", code, fcmToken[^Math.Min(8, fcmToken.Length)..]);
        }
    }
}
