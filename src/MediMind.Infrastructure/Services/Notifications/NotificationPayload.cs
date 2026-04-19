namespace MediMind.Infrastructure.Services.Notifications;

public record NotificationPayload(
    string Title,
    string Body,
    IReadOnlyDictionary<string, string>? Data = null,
    string? ImageUrl = null,
    string Priority = "high",
    string? ClickAction = null);
