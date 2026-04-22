namespace MediMind.Application.Features.NotificationPreferences;

public record NotificationPreferenceResponseDto(
    Guid Id,
    Guid UserId,
    bool AppointmentRemindersPush,
    bool AppointmentRemindersSms,
    bool QueueUpdates,
    bool HealthPredictionReady,
    bool MedicationReminders,
    bool PromotionalEmails,
    DateTime UpdatedAt);

public record UpdateNotificationPreferenceDto(
    bool AppointmentRemindersPush,
    bool AppointmentRemindersSms,
    bool QueueUpdates,
    bool HealthPredictionReady,
    bool MedicationReminders,
    bool PromotionalEmails);
