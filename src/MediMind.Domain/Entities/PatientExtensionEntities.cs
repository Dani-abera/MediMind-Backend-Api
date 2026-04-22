using MediMind.Domain.Common;
using MediMind.Domain.Enums;

namespace MediMind.Domain.Entities;

// ─── Patient Medical History ──────────────────────────────────────────────────

/// <summary>Structured medical background for a patient — one row per patient.</summary>
public class PatientMedicalHistory : BaseEntity
{
    public Guid PatientId { get; private set; }

    /// <summary>JSON array of condition strings.</summary>
    public string ChronicConditions { get; private set; } = "[]";

    /// <summary>JSON array of { allergen, severity, notes } objects.</summary>
    public string Allergies { get; private set; } = "[]";

    /// <summary>JSON array of { name, dosage, startedOn } objects.</summary>
    public string CurrentMedications { get; private set; } = "[]";

    public string? BloodType { get; private set; }
    public bool? Smoker { get; private set; }

    public AlcoholConsumptionLevel? AlcoholConsumption { get; private set; }

    /// <summary>JSON object of family history entries.</summary>
    public string FamilyHistory { get; private set; } = "{}";

    public Patient Patient { get; private set; } = null!;

    private PatientMedicalHistory() { }

    public static PatientMedicalHistory Create(Guid patientId) =>
        new() { PatientId = patientId };

    public void Update(
        string chronicConditions,
        string allergies,
        string currentMedications,
        string? bloodType,
        bool? smoker,
        AlcoholConsumptionLevel? alcoholConsumption,
        string familyHistory)
    {
        ChronicConditions = chronicConditions;
        Allergies = allergies;
        CurrentMedications = currentMedications;
        BloodType = bloodType;
        Smoker = smoker;
        AlcoholConsumption = alcoholConsumption;
        FamilyHistory = familyHistory;
        UpdateTimestamp();
    }

    public void Clear()
    {
        ChronicConditions = "[]";
        Allergies = "[]";
        CurrentMedications = "[]";
        BloodType = null;
        Smoker = null;
        AlcoholConsumption = null;
        FamilyHistory = "{}";
        UpdateTimestamp();
    }
}

// ─── Emergency Contact ────────────────────────────────────────────────────────

/// <summary>Up to 3 emergency contacts per patient. Exactly one must be IsPrimary.</summary>
public class EmergencyContact : BaseEntity
{
    public Guid PatientId { get; private set; }
    public string FullName { get; private set; } = string.Empty;

    public ContactRelationship Relationship { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public bool IsPrimary { get; private set; }

    public Patient Patient { get; private set; } = null!;

    private EmergencyContact() { }

    public static EmergencyContact Create(
        Guid patientId, string fullName, ContactRelationship relationship, string phoneNumber, bool isPrimary) =>
        new()
        {
            PatientId = patientId,
            FullName = fullName,
            Relationship = relationship,
            PhoneNumber = phoneNumber,
            IsPrimary = isPrimary
        };

    public void Update(string fullName, ContactRelationship relationship, string phoneNumber)
    {
        FullName = fullName;
        Relationship = relationship;
        PhoneNumber = phoneNumber;
        UpdateTimestamp();
    }

    public void SetPrimary(bool isPrimary)
    {
        IsPrimary = isPrimary;
        UpdateTimestamp();
    }
}

// ─── Health Record Attachment ─────────────────────────────────────────────────

/// <summary>File attachment (PDF or image) associated with a health record.</summary>
public class HealthRecordAttachment : BaseEntity
{
    public Guid HealthRecordId { get; private set; }
    public string FileName { get; private set; } = string.Empty;
    public string FileType { get; private set; } = string.Empty;
    public long FileSizeBytes { get; private set; }
    public string StoragePath { get; private set; } = string.Empty;
    public DateTime UploadedAt { get; private set; }

    public HealthRecord HealthRecord { get; private set; } = null!;

    private HealthRecordAttachment() { }

    public static HealthRecordAttachment Create(
        Guid healthRecordId, string fileName, string fileType, long fileSizeBytes, string storagePath) =>
        new()
        {
            HealthRecordId = healthRecordId,
            FileName = fileName,
            FileType = fileType,
            FileSizeBytes = fileSizeBytes,
            StoragePath = storagePath,
            UploadedAt = DateTime.UtcNow
        };
}

// ─── Review ───────────────────────────────────────────────────────────────────

/// <summary>Patient review of a doctor or healthcare center after a completed appointment.</summary>
public class Review : BaseEntity
{
    public Guid PatientId { get; private set; }

    /// <summary>Set when reviewing a doctor. Mutually exclusive with CenterId for a single row.</summary>
    public Guid? DoctorId { get; private set; }

    /// <summary>Set when reviewing a center. Mutually exclusive with DoctorId for a single row.</summary>
    public Guid? CenterId { get; private set; }

    public Guid AppointmentId { get; private set; }
    public int Rating { get; private set; }
    public string? Comment { get; private set; }

    public Patient Patient { get; private set; } = null!;
    public Appointment Appointment { get; private set; } = null!;

    private Review() { }

    public static Review CreateDoctorReview(
        Guid patientId, Guid doctorId, Guid appointmentId, int rating, string? comment) =>
        new()
        {
            PatientId = patientId,
            DoctorId = doctorId,
            AppointmentId = appointmentId,
            Rating = rating,
            Comment = comment
        };

    public static Review CreateCenterReview(
        Guid patientId, Guid centerId, Guid appointmentId, int rating, string? comment) =>
        new()
        {
            PatientId = patientId,
            CenterId = centerId,
            AppointmentId = appointmentId,
            Rating = rating,
            Comment = comment
        };
}

// ─── Favorite ─────────────────────────────────────────────────────────────────

/// <summary>Patient-saved favourite doctor or healthcare center.</summary>
public class Favorite : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public Guid? CenterId { get; private set; }

    public Patient Patient { get; private set; } = null!;

    private Favorite() { }

    public static Favorite ForDoctor(Guid patientId, Guid doctorId) =>
        new() { PatientId = patientId, DoctorId = doctorId };

    public static Favorite ForCenter(Guid patientId, Guid centerId) =>
        new() { PatientId = patientId, CenterId = centerId };
}

// ─── Notification Preference ──────────────────────────────────────────────────

/// <summary>Per-user notification channel preferences — one row per user.</summary>
public class NotificationPreference : BaseEntity
{
    public Guid UserId { get; private set; }
    public bool AppointmentRemindersPush { get; private set; } = true;
    public bool AppointmentRemindersSms { get; private set; } = true;
    public bool QueueUpdates { get; private set; } = true;
    public bool HealthPredictionReady { get; private set; } = true;
    public bool MedicationReminders { get; private set; } = true;
    public bool PromotionalEmails { get; private set; } = false;

    private NotificationPreference() { }

    public static NotificationPreference CreateDefault(Guid userId) =>
        new() { UserId = userId };

    public void Update(
        bool appointmentRemindersPush,
        bool appointmentRemindersSms,
        bool queueUpdates,
        bool healthPredictionReady,
        bool medicationReminders,
        bool promotionalEmails)
    {
        AppointmentRemindersPush = appointmentRemindersPush;
        AppointmentRemindersSms = appointmentRemindersSms;
        QueueUpdates = queueUpdates;
        HealthPredictionReady = healthPredictionReady;
        MedicationReminders = medicationReminders;
        PromotionalEmails = promotionalEmails;
        UpdateTimestamp();
    }
}
