namespace MediMind.Application.Notifications;

/// <summary>
/// Predefined notification copy for push + SMS channels.
/// </summary>
public static class NotificationTemplates
{
    public static (string Title, string Body, string SmsMessage) BookingConfirmed(
        string doctorName,
        DateOnly date,
        TimeOnly time,
        string center) =>
        (
            "Appointment booked",
            $"Your appointment with Dr. {doctorName} on {date:MMM dd} at {time:hh\\:mm} at {center} is pending confirmation.",
            $"MediMind: Booking with Dr. {doctorName} on {date:dd/MM} {time:hh\\:mm} at {center}. Pending confirmation.");

    public static (string Title, string Body, string SmsMessage) BookingApproved(
        string doctorName,
        DateOnly date,
        TimeOnly time) =>
        (
            "Appointment approved",
            $"Your appointment with Dr. {doctorName} on {date:MMM dd} at {time:hh\\:mm} has been approved.",
            $"MediMind: Appt with Dr. {doctorName} on {date:dd/MM} {time:hh\\:mm} approved.");

    public static (string Title, string Body, string SmsMessage) BookingRejected(string doctorName, string reason) =>
        (
            "Appointment update",
            $"Your booking with Dr. {doctorName} could not be approved. Reason: {reason}",
            $"MediMind: Booking with Dr. {doctorName} not approved. {reason}");

    public static (string Title, string Body, string SmsMessage) BookingCancelled(
        string doctorName,
        DateOnly date) =>
        (
            "Appointment cancelled",
            $"Your appointment with Dr. {doctorName} on {date:MMM dd} has been cancelled.",
            $"MediMind: Appt with Dr. {doctorName} on {date:dd/MM} cancelled.");

    public static (string Title, string Body, string SmsMessage) Reminder24h(
        string doctorName,
        DateOnly date,
        TimeOnly time,
        string center) =>
        (
            "Appointment tomorrow",
            $"Reminder: Dr. {doctorName} tomorrow ({date:MMM dd}) at {time:hh\\:mm} — {center}.",
            $"MediMind 24h: Dr. {doctorName} tomorrow {date:dd/MM} {time:hh\\:mm} at {center}.");

    public static (string Title, string Body, string SmsMessage) Reminder2h(string doctorName, TimeOnly time) =>
        (
            "Appointment soon",
            $"Your visit with Dr. {doctorName} is in about 2 hours ({time:hh\\:mm}).",
            $"MediMind: Dr. {doctorName} in ~2h at {time:hh\\:mm}.");

    public static (string Title, string Body, string SmsMessage) QueuePosition(
        int position,
        int estimatedWaitMinutes,
        string centerName) =>
        (
            "Queue update",
            $"You are #{position} at {centerName}. Estimated wait ~{estimatedWaitMinutes} min.",
            $"MediMind queue: #{position} at {centerName}. Wait ~{estimatedWaitMinutes} min.");

    public static (string Title, string Body, string SmsMessage) QueueCallingSoon(int position) =>
        (
            "Almost your turn",
            $"You are #{position} — please stay nearby; you will be called soon.",
            $"MediMind: You are #{position}. Please stay close; you'll be called soon.");

    public static (string Title, string Body, string SmsMessage) QueueCalledNow(string room, string centerName) =>
        (
            "Please proceed",
            $"It's your turn at {centerName}. Please go to {room}.",
            $"MediMind: Your turn at {centerName}. Room {room}.");

    public static (string Title, string Body, string SmsMessage) VideoConsultationStarted(string doctorName) =>
        (
            "Video visit started",
            $"Dr. {doctorName} has started your video consultation. Join now.",
            $"MediMind: Video visit with Dr. {doctorName} started. Open the app to join.");

    public static (string Title, string Body, string SmsMessage) HealthPredictionReady(string highestRisk) =>
        (
            "Health insight ready",
            $"Your latest health prediction highlights {highestRisk} as the highest modeled risk. Open MediMind for details.",
            $"MediMind: Health prediction ready. Highest risk: {highestRisk}. Open the app.");

    public static (string Title, string Body, string SmsMessage) MedicationReminder(
        string medicationName,
        string dosage) =>
        (
            "Medication reminder",
            $"Time to take {medicationName} ({dosage}).",
            $"MediMind: Take {medicationName} {dosage}.");
}
