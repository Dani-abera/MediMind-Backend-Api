using MediMind.Domain.Common;

namespace MediMind.Domain.Entities;

// ─── User device (FCM) ───────────────────────────────────────────────────────

public class UserDeviceToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string FcmToken { get; private set; } = string.Empty;
    public string DevicePlatform { get; private set; } = string.Empty;
    public string? DeviceModel { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public DateTime LastUsedAt { get; private set; }
    public bool IsActive { get; private set; }

    public User? User { get; private set; }

    private UserDeviceToken() { }

    public UserDeviceToken(Guid userId, string fcmToken, string devicePlatform, string? deviceModel)
    {
        UserId = userId;
        FcmToken = fcmToken;
        DevicePlatform = devicePlatform;
        DeviceModel = deviceModel;
        var now = DateTime.UtcNow;
        RegisteredAt = now;
        LastUsedAt = now;
        IsActive = true;
    }

    public void Touch()
    {
        LastUsedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }

    public void Reactivate(string devicePlatform, string? deviceModel)
    {
        IsActive = true;
        DevicePlatform = devicePlatform;
        DeviceModel = deviceModel;
        LastUsedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }
}

// ─── Notification audit log ───────────────────────────────────────────────────

public class NotificationLog : BaseEntity
{
    public Guid? UserId { get; private set; }
    public string? PhoneNumber { get; private set; }
    public string NotificationType { get; private set; } = string.Empty;
    public string Channel { get; private set; } = string.Empty;
    public string? Title { get; private set; }
    public string Body { get; private set; } = string.Empty;
    public string Status { get; private set; } = string.Empty;
    public string? ExternalReference { get; private set; }
    public string? ErrorMessage { get; private set; }
    public DateTime SentAt { get; private set; }

    private NotificationLog() { }

    public NotificationLog(
        Guid? userId,
        string? phoneNumber,
        string notificationType,
        string channel,
        string? title,
        string body,
        string status,
        string? externalReference,
        string? errorMessage,
        DateTime sentAt)
    {
        UserId = userId;
        PhoneNumber = phoneNumber;
        NotificationType = notificationType;
        Channel = channel;
        Title = title;
        Body = body;
        Status = status;
        ExternalReference = externalReference;
        ErrorMessage = errorMessage;
        SentAt = sentAt;
    }
}

// ─── Medication reminder ─────────────────────────────────────────────────────

public class MedicationReminder : BaseEntity
{
    public Guid PatientId { get; private set; }
    public string MedicationName { get; private set; } = string.Empty;
    public string Dosage { get; private set; } = string.Empty;
    public string Frequency { get; private set; } = string.Empty;
    /// <summary>JSON array of ISO times, e.g. ["09:00:00","21:00:00"].</summary>
    public string ReminderTimes { get; private set; } = "[]";
    public bool IsActive { get; private set; }

    public Patient Patient { get; private set; } = null!;

    private MedicationReminder() { }

    public MedicationReminder(
        Guid patientId,
        string medicationName,
        string dosage,
        string frequency,
        string reminderTimesJson)
    {
        PatientId = patientId;
        MedicationName = medicationName;
        Dosage = dosage;
        Frequency = frequency;
        ReminderTimes = reminderTimesJson;
        IsActive = true;
    }

    public void Update(string medicationName, string dosage, string frequency, string reminderTimesJson)
    {
        MedicationName = medicationName;
        Dosage = dosage;
        Frequency = frequency;
        ReminderTimes = reminderTimesJson;
        UpdateTimestamp();
    }

    public void SetActive(bool active)
    {
        IsActive = active;
        UpdateTimestamp();
    }
}
