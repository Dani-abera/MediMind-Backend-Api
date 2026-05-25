using MediMind.Domain.Common;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Domain.Entities;

// ─── Refresh Token ────────────────────────────────────────────────────────────

/// <summary>
/// Persisted refresh token (7-day expiry, one-time use).
/// Replaces the previous in-memory dictionary so tokens survive server restarts.
/// </summary>
public class RefreshToken : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }

    // Navigation
    public User User { get; private set; } = null!;

    private RefreshToken() { }

    public RefreshToken(Guid userId, string token, DateTime expiresAt)
    {
        UserId = userId;
        Token = token;
        ExpiresAt = expiresAt;
    }

    public bool IsExpired => ExpiresAt < DateTime.UtcNow;
}

// ─── Queue Entry ──────────────────────────────────────────────────────────────

/// <summary>
/// Maps to `queue` table. COMPOSED inside Appointment (cannot exist without it).
/// Generated daily at 06:00 AM by cron job for all confirmed appointments.
/// </summary>
public class QueueEntry : BaseEntity
{
    public Guid AppointmentId { get; private set; }   // Unique — 1:1 with Appointment
    public Guid CenterId { get; private set; }         // Tenant ID
    public DateOnly QueueDate { get; private set; }
    public string QueueNumber { get; private set; } = string.Empty;  // Q001, Q002...
    public int Position { get; private set; }
    public QueueStatus Status { get; private set; } = QueueStatus.Waiting;
    public int EstimatedWaitTimeMinutes { get; private set; }
    public DateTime? CalledTime { get; private set; }
    public string? RoomNumber { get; private set; }
    public DateTime? ConsultationStartTime { get; private set; }
    public DateTime? ConsultationEndTime { get; private set; }

    // Navigation
    public Appointment Appointment { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;

    private QueueEntry() { }

    public QueueEntry(Guid appointmentId, Guid centerId, DateOnly queueDate, int position, int slotDurationMinutes)
    {
        AppointmentId = appointmentId;
        CenterId = centerId;
        QueueDate = queueDate;
        Position = position;
        QueueNumber = $"Q{position:D3}";  // Q001, Q002, ...
        EstimatedWaitTimeMinutes = (position - 1) * slotDurationMinutes;
    }

    public void CallPatient(string? roomNumber = null)
    {
        if (Status != QueueStatus.Waiting)
            throw new DomainException("Only waiting patients can be called.");

        Status = QueueStatus.Called;
        CalledTime = DateTime.UtcNow;
        RoomNumber = roomNumber;
        UpdateTimestamp();
    }

    public void StartConsultation()
    {
        Status = QueueStatus.InConsultation;
        ConsultationStartTime = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void CompleteConsultation()
    {
        Status = QueueStatus.Completed;
        ConsultationEndTime = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void MarkMissed()
    {
        Status = QueueStatus.Missed;
        UpdateTimestamp();
    }

    public void Skip()
    {
        if (Status != QueueStatus.Waiting && Status != QueueStatus.Called)
            throw new DomainException("Only waiting or called entries can be skipped.");
        Status = QueueStatus.Waiting;
        UpdateTimestamp();
    }

    public void Cancel()
    {
        if (Status == QueueStatus.Completed || Status == QueueStatus.InConsultation)
            throw new DomainException("Cannot cancel a completed or in-progress consultation.");
        Status = QueueStatus.Cancelled;
        UpdateTimestamp();
    }

    public void UpdatePosition(int newPosition, int slotDurationMinutes)
    {
        Position = newPosition;
        EstimatedWaitTimeMinutes = (newPosition - 1) * slotDurationMinutes;
        UpdateTimestamp();
    }
}

// ─── Waitlist Subscription ────────────────────────────────────────────────────

/// <summary>
/// Maps to `waitlist_subscriptions` table.
/// Created when a patient wants to be notified when a slot opens for a fully-booked doctor.
/// </summary>
public class WaitlistSubscription : BaseEntity
{
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }
    public DateOnly PreferredDateFrom { get; private set; }
    public DateOnly PreferredDateTo { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTime? NotifiedAt { get; private set; }

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public Doctor Doctor { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;

    private WaitlistSubscription() { }

    public static WaitlistSubscription Create(
        Guid patientId, Guid doctorId, Guid centerId,
        DateOnly preferredDateFrom, DateOnly preferredDateTo)
    {
        if (preferredDateTo < preferredDateFrom)
            throw new DomainException("PreferredDateTo must be on or after PreferredDateFrom.");
        return new WaitlistSubscription
        {
            PatientId = patientId,
            DoctorId = doctorId,
            CenterId = centerId,
            PreferredDateFrom = preferredDateFrom,
            PreferredDateTo = preferredDateTo
        };
    }

    public void MarkNotified()
    {
        IsActive = false;
        NotifiedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}

// ─── Doctor Schedule ──────────────────────────────────────────────────────────

/// <summary>Maps to `doctor_schedules` table. One schedule per doctor per center.</summary>
public class DoctorSchedule : BaseEntity
{
    public Guid ScheduleId => Id;
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }
    public List<string> WorkingDays { get; private set; } = [];   // ["Monday", "Tuesday", ...]
    public TimeOnly StartTime { get; private set; }
    public TimeOnly EndTime { get; private set; }
    public int SlotDuration { get; private set; }
    public TimeOnly? BreakStart { get; private set; }
    public TimeOnly? BreakEnd { get; private set; }

    // Navigation
    public Doctor Doctor { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;

    private DoctorSchedule() { }

    public DoctorSchedule(
        Guid doctorId,
        Guid centerId,
        List<string> workingDays,
        TimeOnly startTime,
        TimeOnly endTime,
        int slotDuration,
        TimeOnly? breakStart = null,
        TimeOnly? breakEnd = null)
    {
        if (endTime <= startTime)
            throw new DomainException("End time must be after start time.");
        if (breakStart.HasValue && breakEnd.HasValue && breakEnd <= breakStart)
            throw new DomainException("Break end time must be after break start time.");
        if (breakStart.HasValue && breakEnd.HasValue && (breakStart < startTime || breakEnd > endTime))
            throw new DomainException($"Break time must be within working hours [{startTime:HH\\:mm}-{endTime:HH\\:mm}]");

        DoctorId = doctorId;
        CenterId = centerId;
        WorkingDays = workingDays;
        StartTime = startTime;
        EndTime = endTime;
        SlotDuration = slotDuration;
        BreakStart = breakStart;
        BreakEnd = breakEnd;
    }

    public List<TimeOnly> GetAvailableSlots(DateOnly date, IEnumerable<TimeOnly> bookedTimes)
    {
        var dayName = date.DayOfWeek.ToString();
        if (!WorkingDays.Contains(dayName)) return [];

        var slots = new List<TimeOnly>();
        var current = StartTime;

        while (current.Add(TimeSpan.FromMinutes(SlotDuration)) <= EndTime)
        {
            var isDuringBreak = BreakStart.HasValue && BreakEnd.HasValue &&
                                current >= BreakStart && current < BreakEnd;
            var isBooked = bookedTimes.Contains(current);
            var isPast = date == DateOnly.FromDateTime(DateTime.UtcNow) &&
                         current < TimeOnly.FromDateTime(DateTime.UtcNow);

            if (!isDuringBreak && !isBooked && !isPast)
                slots.Add(current);

            current = current.Add(TimeSpan.FromMinutes(SlotDuration));
        }

        return slots;
    }

    public bool IsWorkingDay(DateOnly date) =>
        WorkingDays.Contains(date.DayOfWeek.ToString());
}

// ─── Doctor Invitation ────────────────────────────────────────────────────────

/// <summary>
/// Token-based invitation sent to a doctor by a healthcare center admin.
/// Account is only created after the doctor accepts via the invitation link.
/// </summary>
public class DoctorInvitation : BaseEntity
{
    public Guid CenterId { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string FullName { get; private set; } = string.Empty;
    public string LicenseNumber { get; private set; } = string.Empty;
    public string Specialization { get; private set; } = string.Empty;
    public int YearsOfExperience { get; private set; }
    public decimal ConsultationFee { get; private set; }
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Token { get; private set; } = string.Empty;
    public DateTime ExpiresAt { get; private set; }
    public bool IsAccepted { get; private set; }

    private DoctorInvitation() { }

    public DoctorInvitation(
        Guid centerId,
        string email,
        string fullName,
        string licenseNumber,
        string specialization,
        int yearsOfExperience,
        decimal consultationFee,
        string phoneNumber)
    {
        CenterId = centerId;
        Email = email;
        FullName = fullName;
        LicenseNumber = licenseNumber;
        Specialization = specialization;
        YearsOfExperience = yearsOfExperience;
        ConsultationFee = consultationFee;
        PhoneNumber = phoneNumber;
        Token = Guid.NewGuid().ToString("N");
        ExpiresAt = DateTime.UtcNow.AddHours(48);
    }

    public void Accept()
    {
        IsAccepted = true;
        UpdateTimestamp();
    }
}

// ─── Schedule Exception ───────────────────────────────────────────────────────

/// <summary>
/// Marks a specific date as unavailable for a doctor at a given center (out-of-office, leave, etc.).
/// Causes GetAvailableSlotsAsync to return empty for that date.
/// </summary>
public class ScheduleException : BaseEntity
{
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }
    public DateOnly ExceptionDate { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    private ScheduleException() { }

    public ScheduleException(Guid doctorId, Guid centerId, DateOnly date, string reason)
    {
        DoctorId = doctorId;
        CenterId = centerId;
        ExceptionDate = date;
        Reason = reason;
    }
}

// ─── Health Record ────────────────────────────────────────────────────────────

/// <summary>
/// Maps to `health_records` table.
/// All vitals are nullable — patients input what they have measured today.
/// Validation ranges enforced via CHECK constraints and domain rules.
/// </summary>
public class HealthRecord : BaseEntity
{
    // Compatibility alias for API contract language ("RecordId")
    public Guid RecordId => Id;
    public Guid PatientId { get; private set; }
    public DateOnly RecordDate { get; private set; }
    public TimeOnly RecordTime { get; private set; } = TimeOnly.FromDateTime(DateTime.UtcNow);

    // Vitals — ranges from DB schema CHECK constraints
    public int? SystolicBp { get; private set; }        // 70–250 mmHg
    public int? DiastolicBp { get; private set; }       // 40–150 mmHg
    public decimal? GlucoseLevel { get; private set; }  // 30–600 mg/dL
    public decimal? Weight { get; private set; }        // 20–300 kg
    public decimal? Height { get; private set; }        // 50–250 cm
    public decimal? Temperature { get; private set; }   // 35–43 °C
    public int? HeartRate { get; private set; }         // 30–250 bpm
    public int? OxygenSaturation { get; private set; }  // 70–100 %
    public int? RespiratoryRate { get; private set; }   // 8–60 bpm
    public string? Notes { get; private set; }
    public string RecordedBy { get; private set; } = "patient"; // patient | doctor

    // Computed
    public decimal? Bmi => (Weight.HasValue && Height.HasValue && Height > 0)
        ? Math.Round(Weight.Value / ((Height.Value / 100) * (Height.Value / 100)), 2)
        : null;

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public ICollection<HealthRecordAttachment> Attachments { get; private set; } = [];

    private HealthRecord() { }

    public static HealthRecord Create(
        Guid patientId,
        DateOnly recordDate,
        TimeOnly? recordTime,
        int? systolicBp, int? diastolicBp,
        decimal? glucoseLevel, decimal? weight, decimal? height,
        decimal? temperature, int? heartRate,
        int? oxygenSaturation, int? respiratoryRate,
        string? notes, string recordedBy = "patient")
    {
        // Domain validation
        if (systolicBp.HasValue && (systolicBp < 70 || systolicBp > 250))
            throw new DomainException("Systolic BP must be between 70 and 250 mmHg.");
        if (diastolicBp.HasValue && (diastolicBp < 40 || diastolicBp > 150))
            throw new DomainException("Diastolic BP must be between 40 and 150 mmHg.");
        if (systolicBp.HasValue && diastolicBp.HasValue && systolicBp <= diastolicBp)
            throw new DomainException("Systolic BP must be greater than Diastolic BP.");
        if (glucoseLevel.HasValue && (glucoseLevel < 30 || glucoseLevel > 600))
            throw new DomainException("Glucose level must be between 30 and 600 mg/dL.");
        if (temperature.HasValue && (temperature < 35 || temperature > 43))
            throw new DomainException("Temperature must be between 35 and 43 °C.");
        if (heartRate.HasValue && (heartRate < 30 || heartRate > 250))
            throw new DomainException("Heart rate must be between 30 and 250 bpm.");

        return new HealthRecord
        {
            PatientId = patientId,
            RecordDate = recordDate,
            RecordTime = recordTime ?? TimeOnly.FromDateTime(DateTime.UtcNow),
            SystolicBp = systolicBp,
            DiastolicBp = diastolicBp,
            GlucoseLevel = glucoseLevel,
            Weight = weight,
            Height = height,
            Temperature = temperature,
            HeartRate = heartRate,
            OxygenSaturation = oxygenSaturation,
            RespiratoryRate = respiratoryRate,
            Notes = notes,
            RecordedBy = recordedBy
        };
    }

    public void UpdateVitals(
        DateOnly recordDate,
        TimeOnly? recordTime,
        int? systolicBp,
        int? diastolicBp,
        decimal? glucoseLevel,
        decimal? weight,
        decimal? height,
        decimal? temperature,
        int? heartRate,
        int? oxygenSaturation,
        int? respiratoryRate,
        string? notes,
        string recordedBy)
    {
        RecordDate = recordDate;
        RecordTime = recordTime ?? RecordTime;
        SystolicBp = systolicBp;
        DiastolicBp = diastolicBp;
        GlucoseLevel = glucoseLevel;
        Weight = weight;
        Height = height;
        Temperature = temperature;
        HeartRate = heartRate;
        OxygenSaturation = oxygenSaturation;
        RespiratoryRate = respiratoryRate;
        Notes = notes;
        RecordedBy = recordedBy;
        UpdateTimestamp();
    }
}

// ─── Health Prediction ────────────────────────────────────────────────────────

/// <summary>Maps to `health_predictions` table. AI-generated risk assessments.</summary>
public class HealthPrediction : BaseEntity
{
    public Guid PredictionId => Id;
    public Guid PatientId { get; private set; }
    public DateOnly PredictionDate { get; private set; }
    public TimeOnly PredictionTime { get; private set; }

    // Risk scores: 0.00–100.00 %
    public decimal DiabetesRisk { get; private set; }
    public RiskCategory DiabetesCategory { get; private set; }
    public decimal HypertensionRisk { get; private set; }
    public RiskCategory HypertensionCategory { get; private set; }
    public decimal CvdRisk { get; private set; }
    public RiskCategory CvdCategory { get; private set; }

    public string ModelVersion { get; private set; } = string.Empty;
    public decimal Confidence { get; private set; }
    public Dictionary<string, List<string>> ContributingFactors { get; private set; } = [];
    public string Recommendations { get; private set; } = string.Empty;
    public int DataPointsUsed { get; private set; }

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public ICollection<HealthPredictionRecord> PredictionRecords { get; private set; } = [];

    private HealthPrediction() { }

    public static HealthPrediction Create(
        Guid patientId,
        decimal diabetesRisk, decimal hypertensionRisk, decimal cvdRisk,
        decimal confidence, string modelVersion,
        Dictionary<string, List<string>> contributingFactors,
        string recommendations,
        int dataPointsUsed)
    {
        return new HealthPrediction
        {
            PatientId = patientId,
            PredictionDate = DateOnly.FromDateTime(DateTime.UtcNow),
            PredictionTime = TimeOnly.FromDateTime(DateTime.UtcNow),
            DiabetesRisk = diabetesRisk,
            DiabetesCategory = CategorizeRisk(diabetesRisk),
            HypertensionRisk = hypertensionRisk,
            HypertensionCategory = CategorizeRisk(hypertensionRisk),
            CvdRisk = cvdRisk,
            CvdCategory = CategorizeRisk(cvdRisk),
            ModelVersion = modelVersion,
            Confidence = confidence,
            ContributingFactors = contributingFactors,
            Recommendations = recommendations,
            DataPointsUsed = dataPointsUsed
        };
    }

    private static RiskCategory CategorizeRisk(decimal risk) =>
        risk <= 33 ? RiskCategory.Low :
        risk <= 66 ? RiskCategory.Medium :
        RiskCategory.High;
}

/// <summary>Junction: HealthPrediction ↔ HealthRecord (many-to-many).</summary>
public class HealthPredictionRecord
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid PredictionId { get; private set; }
    public Guid RecordId { get; private set; }
    public HealthPrediction Prediction { get; private set; } = null!;
    public HealthRecord Record { get; private set; } = null!;

    private HealthPredictionRecord() { }

    public HealthPredictionRecord(Guid predictionId, Guid recordId)
    {
        PredictionId = predictionId;
        RecordId = recordId;
    }
}

// ─── Prescription ─────────────────────────────────────────────────────────────

public class Prescription : BaseEntity
{
    public Guid AppointmentId { get; private set; }
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }
    public DateOnly IssueDate { get; private set; }
    public DateOnly? ExpiryDate { get; private set; }
    public string Diagnosis { get; private set; } = string.Empty;

    // JSON: [{"name":"...","dosage":"...","frequency":"...","duration":"..."}]
    public List<MedicationItem> Medications { get; private set; } = [];
    public List<string> LabTests { get; private set; } = [];
    public string? FollowUpInstructions { get; private set; }
    public string? SpecialInstructions { get; private set; }
    public string? PrescriptionUrl { get; private set; }
    public string? QrCode { get; private set; }
    public PrescriptionStatus Status { get; private set; } = PrescriptionStatus.Active;

    // Navigation
    public Appointment Appointment { get; private set; } = null!;
    public Patient Patient { get; private set; } = null!;
    public Doctor Doctor { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;

    private Prescription() { }

    public static Prescription Issue(
        Guid appointmentId, Guid patientId, Guid doctorId, Guid centerId,
        string diagnosis, List<MedicationItem> medications,
        List<string>? labTests = null, string? followUpInstructions = null,
        string? specialInstructions = null, DateOnly? expiryDate = null)
    {
        var issueDate = DateOnly.FromDateTime(DateTime.UtcNow);
        if (expiryDate.HasValue && expiryDate.Value <= issueDate)
            throw new DomainException("Expiry date must be after the issue date.");

        return new Prescription
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            DoctorId = doctorId,
            CenterId = centerId,
            IssueDate = issueDate,
            ExpiryDate = expiryDate,
            Diagnosis = diagnosis,
            Medications = medications,
            LabTests = labTests ?? [],
            FollowUpInstructions = followUpInstructions,
            SpecialInstructions = specialInstructions
        };
    }

    public void SetDocumentUrl(string url, string qrCode)
    {
        PrescriptionUrl = url;
        QrCode = qrCode;
        UpdateTimestamp();
    }

    public void SetPrescriptionUrl(string url)
    {
        PrescriptionUrl = url;
        UpdateTimestamp();
    }

    public void SetQrCode(string qrCodeDataUrl)
    {
        QrCode = qrCodeDataUrl;
        UpdateTimestamp();
    }

    public void MarkDispensed()
    {
        Status = PrescriptionStatus.Dispensed;
        UpdateTimestamp();
    }

    public void MarkCancelled()
    {
        Status = PrescriptionStatus.Cancelled;
        UpdateTimestamp();
    }

    public void SetStatus(PrescriptionStatus status)
    {
        Status = status;
        UpdateTimestamp();
    }
}

/// <summary>Serialized to JSON in <see cref="Prescription.Medications"/>.</summary>
public record MedicationItem(
    string Name,
    string Dosage,
    string Frequency,
    string Duration,
    string? Instructions = null);

// ─── Video Consultation ───────────────────────────────────────────────────────

public class VideoConsultation : BaseEntity
{
    public Guid ConsultationId => Id;
    public Guid AppointmentId { get; private set; }  // Unique — 1:0..1 with Appointment
    public string RoomId { get; private set; } = string.Empty;
    public VideoConsultationStatus Status { get; private set; } = VideoConsultationStatus.Scheduled;
    public DateTime? StartTime { get; private set; }
    public DateTime? EndTime { get; private set; }
    public int? DurationMinutes { get; private set; }
    public string? VideoQuality { get; private set; }

    // Navigation
    public Appointment Appointment { get; private set; } = null!;
    public ICollection<VideoConsultationParticipant> Participants { get; private set; } = [];

    private VideoConsultation() { }

    public static VideoConsultation Create(Guid appointmentId)
    {
        return new VideoConsultation
        {
            AppointmentId = appointmentId,
            RoomId = $"room_{Guid.NewGuid():N}"
        };
    }

    public void Start()
    {
        Status = VideoConsultationStatus.InProgress;
        StartTime ??= DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void End(string? videoQuality = null)
    {
        Status = VideoConsultationStatus.Completed;
        EndTime ??= DateTime.UtcNow;
        VideoQuality = videoQuality;
        if (StartTime.HasValue)
            DurationMinutes = (int)(EndTime.Value - StartTime.Value).TotalMinutes;
        UpdateTimestamp();
    }

    public void Cancel()
    {
        Status = VideoConsultationStatus.Cancelled;
        EndTime = DateTime.UtcNow;
        if (StartTime.HasValue)
            DurationMinutes = (int)(EndTime.Value - StartTime.Value).TotalMinutes;
        UpdateTimestamp();
    }

    public void ReportQuality(string? quality)
    {
        VideoQuality = quality;
        UpdateTimestamp();
    }
}

public class VideoConsultationParticipant : BaseEntity
{
    public Guid ConsultationId { get; private set; }
    public Guid? PatientId { get; private set; }
    public Guid? DoctorId { get; private set; }
    public DateTime? JoinedAt { get; private set; }
    public DateTime? LeftAt { get; private set; }

    private VideoConsultationParticipant() { }

    public VideoConsultationParticipant(Guid consultationId, Guid? patientId, Guid? doctorId)
    {
        if (!patientId.HasValue && !doctorId.HasValue)
            throw new DomainException("At least one of PatientId or DoctorId must be provided.");

        ConsultationId = consultationId;
        PatientId = patientId;
        DoctorId = doctorId;
        JoinedAt = DateTime.UtcNow;
    }

    public void Leave()
    {
        LeftAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public bool MatchesUser(Guid userId) =>
        (PatientId.HasValue && PatientId.Value == userId) ||
        (DoctorId.HasValue && DoctorId.Value == userId);
}

public class ChatMessage : BaseEntity
{
    public Guid MessageId => Id;
    public Guid ConsultationId { get; private set; }
    public Guid SenderId { get; private set; }
    public string SenderType { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public DateTime SentAt { get; private set; } = DateTime.UtcNow;
    public bool IsRead { get; private set; }

    public VideoConsultation Consultation { get; private set; } = null!;

    private ChatMessage() { }

    public ChatMessage(Guid consultationId, Guid senderId, string senderType, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new DomainException("Message content cannot be empty.");
        if (content.Length > 2000)
            throw new DomainException("Message content exceeds 2000 characters.");

        ConsultationId = consultationId;
        SenderId = senderId;
        SenderType = senderType;
        Content = content.Trim();
        SentAt = DateTime.UtcNow;
        IsRead = false;
    }

    public void MarkRead()
    {
        IsRead = true;
        UpdateTimestamp();
    }
}

public class VideoQualityMetric : BaseEntity
{
    public Guid ConsultationId { get; private set; }
    public Guid UserId { get; private set; }
    public int BandwidthKbps { get; private set; }
    public int PacketsLost { get; private set; }
    public int FrameRate { get; private set; }
    public DateTime ReportedAt { get; private set; } = DateTime.UtcNow;

    public VideoConsultation Consultation { get; private set; } = null!;

    private VideoQualityMetric() { }

    public VideoQualityMetric(Guid consultationId, Guid userId, int bandwidthKbps, int packetsLost, int frameRate)
    {
        ConsultationId = consultationId;
        UserId = userId;
        BandwidthKbps = bandwidthKbps;
        PacketsLost = packetsLost;
        FrameRate = frameRate;
        ReportedAt = DateTime.UtcNow;
    }
}

// ─── Payment ──────────────────────────────────────────────────────────────────

public class Payment : BaseEntity
{
    public Guid PaymentId => Id;
    public Guid? AppointmentId { get; private set; }
    public Guid? PatientId { get; private set; }
    public Guid? AdminId { get; private set; }
    public Guid CenterId { get; private set; }
    public string PaymentRef { get; private set; } = string.Empty;

    // Fee breakdown — all stored at creation time, never recomputed
    public decimal Amount { get; private set; }           // = TotalAmount (kept for backwards compat)
    public decimal BaseAmount { get; private set; }
    public decimal VatPercent { get; private set; }
    public decimal VatFee { get; private set; }
    public decimal ServicePercent { get; private set; }
    public decimal ServiceFee { get; private set; }
    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = "ETB";

    public DateTime? PaymentDate { get; private set; }
    public string PaymentMethod { get; private set; } = "Mobile Money";
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public PaymentReasonType ReasonType { get; private set; } = PaymentReasonType.Appointment;
    public string? ChapaTransactionId { get; private set; }
    public string? ChapaCheckoutUrl { get; private set; }
    public DateTime? WebhookReceivedAt { get; private set; }
    public string? ReceiptUrl { get; private set; }

    // Navigation
    public Appointment? Appointment { get; private set; }
    public Patient? Patient { get; private set; }
    public List<PaymentActivity> Activities { get; private set; } = [];

    private Payment() { }

    /// <summary>Legacy factory — no fee breakdown (base amount = total).</summary>
    public static Payment Initiate(
        Guid appointmentId,
        Guid patientId,
        decimal amount,
        string paymentMethod,
        string? paymentRef = null)
    {
        if (amount <= 0)
            throw new DomainException("Payment amount must be greater than 0 ETB.");
        if (string.IsNullOrWhiteSpace(paymentMethod))
            throw new DomainException("Payment method is required.");

        return new Payment
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            PaymentRef = paymentRef ?? $"PAY{DateTime.UtcNow:yyyyMMddHHmmssfff}",
            Amount = amount,
            BaseAmount = amount,
            TotalAmount = amount,
            PaymentMethod = paymentMethod,
            Status = PaymentStatus.Pending
        };
    }

    public static Payment Initiate(Guid appointmentId, Guid patientId, decimal amount, PaymentMethod method) =>
        Initiate(appointmentId, patientId, amount, method switch
        {
            MediMind.Domain.Enums.PaymentMethod.MobileMoney => "Mobile Money",
            MediMind.Domain.Enums.PaymentMethod.Card => "Card",
            MediMind.Domain.Enums.PaymentMethod.Cash => "Cash",
            _ => "Mobile Money"
        });

    /// <summary>Full factory with VAT + service fee breakdown and tenant isolation.</summary>
    public static Payment InitiateWithFees(
        Guid appointmentId,
        Guid patientId,
        Guid centerId,
        decimal baseAmount,
        decimal vatPercent,
        decimal vatFee,
        decimal servicePercent,
        decimal serviceFee,
        decimal totalAmount,
        string paymentRef,
        PaymentReasonType reasonType = PaymentReasonType.Appointment)
    {
        if (baseAmount <= 0)
            throw new DomainException("Payment base amount must be greater than 0 ETB.");

        return new Payment
        {
            AppointmentId = appointmentId,
            PatientId = patientId,
            CenterId = centerId,
            PaymentRef = paymentRef,
            Amount = totalAmount,
            BaseAmount = baseAmount,
            VatPercent = vatPercent,
            VatFee = vatFee,
            ServicePercent = servicePercent,
            ServiceFee = serviceFee,
            TotalAmount = totalAmount,
            PaymentMethod = "Mobile Money",
            Status = PaymentStatus.Pending,
            ReasonType = reasonType
        };
    }

    public static Payment InitiateSubscription(
        Guid centerId,
        Guid adminId,
        decimal baseAmount,
        decimal vatPercent,
        decimal vatFee,
        decimal servicePercent,
        decimal serviceFee,
        decimal totalAmount,
        string paymentRef)
    {
        if (baseAmount <= 0)
            throw new DomainException("Subscription payment base amount must be greater than 0 ETB.");

        return new Payment
        {
            CenterId = centerId,
            AdminId = adminId,
            AppointmentId = null,
            PatientId = null,
            PaymentRef = paymentRef,
            Amount = totalAmount,
            BaseAmount = baseAmount,
            VatPercent = vatPercent,
            VatFee = vatFee,
            ServicePercent = servicePercent,
            ServiceFee = serviceFee,
            TotalAmount = totalAmount,
            PaymentMethod = "Mobile Money",
            Status = PaymentStatus.Pending,
            ReasonType = PaymentReasonType.Subscription
        };
    }

    public void Complete(string? chapaTransactionId)
    {
        Status = PaymentStatus.Completed;
        PaymentDate = DateTime.UtcNow;
        ChapaTransactionId = chapaTransactionId;
        UpdateTimestamp();
    }

    public void MarkFailed()
    {
        Status = PaymentStatus.Failed;
        UpdateTimestamp();
    }

    public void Refund()
    {
        if (Status != PaymentStatus.Completed)
            throw new DomainException("Only completed payments can be refunded.");
        Status = PaymentStatus.Refunded;
        UpdateTimestamp();
    }

    public void SetCheckoutUrl(string? checkoutUrl)
    {
        ChapaCheckoutUrl = checkoutUrl;
        UpdateTimestamp();
    }

    public void MarkWebhookReceived()
    {
        WebhookReceivedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void SetReceiptUrl(string receiptUrl)
    {
        ReceiptUrl = receiptUrl;
        UpdateTimestamp();
    }
}

// ─── Payment Activity (append-only audit log per attempt) ─────────────────────

public class PaymentActivity : BaseEntity
{
    public Guid PaymentId { get; private set; }
    public PaymentAction Action { get; private set; }
    public PaymentStatus Status { get; private set; }
    public string? ChapaTransactionId { get; private set; }
    public string? PaymentRef { get; private set; }
    public decimal TotalAmount { get; private set; }
    public decimal AmountPaid { get; private set; }
    public decimal ChargePaid { get; private set; }
    public string Currency { get; private set; } = "ETB";
    public string? PaymentMethodRaw { get; private set; }
    public string? GatewayMessage { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }

    public Payment Payment { get; private set; } = null!;

    private PaymentActivity() { }

    public static PaymentActivity Create(
        Guid paymentId,
        PaymentAction action,
        PaymentStatus status,
        string? paymentRef,
        decimal totalAmount,
        string currency = "ETB",
        string? chapaTransactionId = null,
        string? paymentMethodRaw = null,
        string? gatewayMessage = null,
        decimal amountPaid = 0,
        decimal chargePaid = 0,
        DateTime? confirmedAt = null)
    {
        return new PaymentActivity
        {
            PaymentId = paymentId,
            Action = action,
            Status = status,
            ChapaTransactionId = chapaTransactionId,
            PaymentRef = paymentRef,
            TotalAmount = totalAmount,
            AmountPaid = amountPaid,
            ChargePaid = chargePaid,
            Currency = currency,
            PaymentMethodRaw = paymentMethodRaw,
            GatewayMessage = gatewayMessage,
            ConfirmedAt = confirmedAt
        };
    }
}

// ─── Subscription Plan (master data, SuperAdmin managed) ──────────────────────

public class SubscriptionPlan : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public SubscriptionPlanTier Tier { get; private set; }
    public decimal MonthlyPrice { get; private set; }
    public decimal YearlyPrice { get; private set; }
    public int MaxDoctors { get; private set; }
    public int MaxAppointmentsPerDay { get; private set; }
    public List<string> Features { get; private set; } = [];
    public string? Description { get; private set; }
    public bool IsActive { get; private set; } = true;

    public ICollection<HealthcareCenter> Centers { get; private set; } = [];

    private SubscriptionPlan() { }

    public static SubscriptionPlan Create(
        string name,
        SubscriptionPlanTier tier,
        decimal monthlyPrice,
        decimal yearlyPrice,
        int maxDoctors,
        int maxAppointmentsPerDay,
        List<string> features,
        string? description = null)
    {
        return new SubscriptionPlan
        {
            Name = name,
            Tier = tier,
            MonthlyPrice = monthlyPrice,
            YearlyPrice = yearlyPrice,
            MaxDoctors = maxDoctors,
            MaxAppointmentsPerDay = maxAppointmentsPerDay,
            Features = features,
            Description = description
        };
    }

    public void UpdatePricing(decimal monthlyPrice, decimal yearlyPrice, int maxDoctors, int maxAppointmentsPerDay, string? description)
    {
        MonthlyPrice = monthlyPrice;
        YearlyPrice = yearlyPrice;
        MaxDoctors = maxDoctors;
        MaxAppointmentsPerDay = maxAppointmentsPerDay;
        Description = description;
        UpdateTimestamp();
    }

    public void Deactivate() { IsActive = false; UpdateTimestamp(); }
    public void Activate()   { IsActive = true;  UpdateTimestamp(); }
}

// ─── Prescription Template ────────────────────────────────────────────────────

public class PrescriptionTemplate : BaseEntity
{
    public Guid TemplateId => Id;
    public Guid DoctorId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Diagnosis { get; private set; }
    // JSON — same schema as Prescription.Medications
    public string Medications { get; private set; } = "[]";
    public string? LabTests { get; private set; }
    public string? FollowUpInstructions { get; private set; }
    public int UseCount { get; private set; }

    public Doctor Doctor { get; private set; } = null!;

    private PrescriptionTemplate() { }

    public static PrescriptionTemplate Create(
        Guid doctorId,
        string name,
        string? description,
        string? diagnosis,
        string medications,
        string? labTests,
        string? followUpInstructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Domain.Exceptions.DomainException("Template name is required.");

        return new PrescriptionTemplate
        {
            DoctorId = doctorId,
            Name = name.Trim(),
            Description = description?.Trim(),
            Diagnosis = diagnosis?.Trim(),
            Medications = string.IsNullOrWhiteSpace(medications) ? "[]" : medications,
            LabTests = labTests,
            FollowUpInstructions = followUpInstructions
        };
    }

    public void Update(
        string name,
        string? description,
        string? diagnosis,
        string medications,
        string? labTests,
        string? followUpInstructions)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new Domain.Exceptions.DomainException("Template name is required.");

        Name = name.Trim();
        Description = description?.Trim();
        Diagnosis = diagnosis?.Trim();
        Medications = string.IsNullOrWhiteSpace(medications) ? "[]" : medications;
        LabTests = labTests;
        FollowUpInstructions = followUpInstructions;
        UpdateTimestamp();
    }

    public void IncrementUseCount()
    {
        UseCount++;
        UpdateTimestamp();
    }
}

// ─── Appointment Note ─────────────────────────────────────────────────────────

public class AppointmentNote : BaseEntity
{
    public Guid NoteId => Id;
    public Guid AppointmentId { get; private set; }
    public Guid DoctorId { get; private set; }
    public string Content { get; private set; } = string.Empty;

    public Appointment Appointment { get; private set; } = null!;
    public Doctor Doctor { get; private set; } = null!;

    private AppointmentNote() { }

    public static AppointmentNote Create(Guid appointmentId, Guid doctorId, string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new Domain.Exceptions.DomainException("Note content cannot be empty.");
        if (content.Length > 5000)
            throw new Domain.Exceptions.DomainException("Note content exceeds 5000 characters.");

        return new AppointmentNote
        {
            AppointmentId = appointmentId,
            DoctorId = doctorId,
            Content = content.Trim()
        };
    }

    public void UpdateContent(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            throw new Domain.Exceptions.DomainException("Note content cannot be empty.");
        if (content.Length > 5000)
            throw new Domain.Exceptions.DomainException("Note content exceeds 5000 characters.");

        Content = content.Trim();
        UpdateTimestamp();
    }
}
