namespace MediMind.Domain.Enums;

// ─── User ────────────────────────────────────────────────────────────────────

public enum UserType
{
    Patient,
    Doctor,
    Admin,
    SuperAdmin
}

public enum UserStatus
{
    Pending,
    Active,
    Inactive,
    Suspended
}

public enum Gender
{
    Male,
    Female,
    NotMentioned
}

// ─── Healthcare Center ────────────────────────────────────────────────────────

public enum SubscriptionStatus
{
    PendingApproval,
    Trial,
    Active,
    Suspended,
    Expired,
    Rejected,
    AwaitingActivation
}

public enum SubscriptionBillingCycle
{
    Monthly = 1,
    Yearly = 2
}

public enum SlotDurationMinutes
{
    Fifteen = 15,
    Thirty = 30,
    FortyFive = 45,
    Sixty = 60
}

// ─── Appointment ─────────────────────────────────────────────────────────────

public enum AppointmentType
{
    InPerson,
    VideoConsultation
}

public enum AppointmentStatus
{
    Pending,
    Confirmed,
    Cancelled,
    Completed,
    NoShow,
    InProgress
}

// ─── Queue ────────────────────────────────────────────────────────────────────

public enum QueueStatus
{
    Waiting,
    Called,
    InConsultation,
    Completed,
    Missed,
    Cancelled
}

// ─── Health Prediction ────────────────────────────────────────────────────────

public enum RiskCategory
{
    Low,      // 0–33%
    Medium,   // 34–66%
    High      // 67–100%
}

// ─── Video Consultation ───────────────────────────────────────────────────────

public enum ConsultationStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

public enum VideoConsultationStatus
{
    Scheduled,
    InProgress,
    Completed,
    Cancelled
}

// ─── Payment ─────────────────────────────────────────────────────────────────

public enum PaymentStatus
{
    Pending,
    Completed,
    Failed,
    Refunded,
    Initialized,
    Cancelled,
    Expired,
    Free
}

public enum PaymentMethod
{
    MobileMoney,   // Telebirr, M-Pesa
    Card,
    Cash
}

public enum PaymentAction
{
    Charge,
    Refund,
    Void,
    Authorize
}

public enum PaymentReasonType
{
    Appointment,
    Subscription
}

// ─── Subscription Plan ────────────────────────────────────────────────────────

public enum SubscriptionPlanTier
{
    Trial    = 10,
    Basic    = 20,
    Standard = 30,
    Premium  = 40
}

// ─── Prescription ─────────────────────────────────────────────────────────────

public enum PrescriptionStatus
{
    Active,
    Dispensed,
    Expired,
    Cancelled
}

// ─── Blood Type ───────────────────────────────────────────────────────────────

public enum BloodType
{
    APositive,
    ANegative,
    BPositive,
    BNegative,
    ABPositive,
    ABNegative,
    OPositive,
    ONegative
}

// ─── Patient Extensions ───────────────────────────────────────────────────────

public enum ContactRelationship
{
    Spouse,
    Parent,
    Sibling,
    Friend,
    Other
}

public enum AlcoholConsumptionLevel
{
    None,
    Occasional,
    Regular
}
