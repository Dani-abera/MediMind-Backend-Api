using MediMind.Domain.Common;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Domain.Entities;

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

    public void CallPatient()
    {
        if (Status != QueueStatus.Waiting)
            throw new DomainException("Only waiting patients can be called.");

        Status = QueueStatus.Called;
        CalledTime = DateTime.UtcNow;
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

    public void UpdatePosition(int newPosition, int slotDurationMinutes)
    {
        Position = newPosition;
        EstimatedWaitTimeMinutes = (newPosition - 1) * slotDurationMinutes;
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
        if (breakStart.HasValue && (breakStart < startTime || breakStart > endTime))
            throw new DomainException("Break must be within working hours.");

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
    public Guid AppointmentId { get; private set; }
    public Guid PatientId { get; private set; }
    public string PaymentRef { get; private set; } = string.Empty;
    public decimal Amount { get; private set; }
    public DateTime? PaymentDate { get; private set; }
    public string PaymentMethod { get; private set; } = "Mobile Money";
    public PaymentStatus Status { get; private set; } = PaymentStatus.Pending;
    public string? ChapaTransactionId { get; private set; }
    public string? ChapaCheckoutUrl { get; private set; }
    public DateTime? WebhookReceivedAt { get; private set; }
    public string? ReceiptUrl { get; private set; }

    // Navigation
    public Appointment Appointment { get; private set; } = null!;
    public Patient Patient { get; private set; } = null!;

    private Payment() { }

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
