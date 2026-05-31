using MediMind.Domain.Common;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Domain.Entities;

/// <summary>
/// Maps to `healthcare_centers` table.
/// Each center is a TENANT in the multi-tenant architecture.
/// center_id == tenant_id for all data isolation.
/// </summary>
public class HealthcareCenter : BaseEntity
{
    public string CenterName { get; private set; } = string.Empty;
    public string CenterType { get; private set; } = string.Empty; // Hospital, Clinic, Health Center
    public string LicenseNumber { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public string City { get; private set; } = string.Empty;
    public string Region { get; private set; } = string.Empty;
    public string PhoneNumber { get; private set; } = string.Empty;
    public string Email { get; private set; } = string.Empty;

    // JSON column: {"Monday": "08:00-17:00", "Tuesday": "08:00-17:00", ...}
    public Dictionary<string, string> WorkingHours { get; private set; } = [];
    public List<string> ServicesOffered { get; private set; } = [];
    public List<string> Specializations { get; private set; } = [];

    // Subscription
    public SubscriptionStatus SubscriptionStatus { get; private set; } = SubscriptionStatus.PendingApproval;
    public string? CurrentPlan { get; private set; }
    public Guid? SubscriptionPlanId { get; private set; }
    public DateOnly? SubscriptionStartDate { get; private set; }
    public DateOnly? SubscriptionEndDate { get; private set; }
    public string? RejectionReason { get; private set; }

    // Pending subscription payment (admin selected plan, awaiting SuperAdmin verification)
    public string? PendingPaymentRef { get; private set; }
    public SubscriptionBillingCycle? PendingBillingCycle { get; private set; }

    // Soft-delete (GDPR NFR-009)
    public bool IsDeleted { get; private set; }
    public DateTime? DeletedAt { get; private set; }

    // Configuration — customizable per tenant
    public int SlotDurationMinutes { get; private set; } = 30;
    public int AdvanceBookingDays { get; private set; } = 30;   // 1–90
    public int CancellationHours { get; private set; } = 2;     // Min hours before appointment
    public bool AutoApproveAppointments { get; private set; } = false;
    public bool RequiresPaymentBeforeConfirmation { get; private set; } = false;

    // GPS
    public decimal? Latitude { get; private set; }
    public decimal? Longitude { get; private set; }

    /// <summary>Public URL for center logo image.</summary>
    public string? ProfileImageUrl { get; private set; }

    /// <summary>Public URL for center cover/banner image.</summary>
    public string? CoverImageUrl { get; private set; }

    /// <summary>Short public description shown on the center's profile.</summary>
    public string? Description { get; private set; }

    // Navigation
    public ICollection<HealthcareCenterAdmin> Admins { get; private set; } = [];
    public ICollection<DoctorHealthcareCenter> DoctorHealthcareCenters { get; private set; } = [];
    public ICollection<Appointment> Appointments { get; private set; } = [];
    public ICollection<QueueEntry> QueueEntries { get; private set; } = [];
    public ICollection<DoctorSchedule> DoctorSchedules { get; private set; } = [];
    public ICollection<SubscriptionHistory> SubscriptionHistories { get; private set; } = [];

    private HealthcareCenter() { }

    public HealthcareCenter(
        string centerName,
        string centerType,
        string licenseNumber,
        string address,
        string city,
        string region,
        string phoneNumber,
        string email,
        Dictionary<string, string> workingHours,
        List<string> servicesOffered)
    {
        CenterName = centerName;
        CenterType = centerType;
        LicenseNumber = licenseNumber;
        Address = address;
        City = city;
        Region = region;
        PhoneNumber = phoneNumber;
        Email = email;
        WorkingHours = workingHours;
        ServicesOffered = servicesOffered;
    }

    public void UpdateGeneralInfo(
        string name, string phone, string email,
        string city, string region, string address,
        List<string> services, List<string> specializations)
    {
        CenterName = name;
        PhoneNumber = phone;
        Email = email;
        City = city;
        Region = region;
        Address = address;
        ServicesOffered = services;
        Specializations = specializations;
        UpdateTimestamp();
    }

    public void UpdateConfiguration(
        int slotDurationMinutes,
        int advanceBookingDays,
        int cancellationHours,
        bool autoApproveAppointments,
        bool requiresPaymentBeforeConfirmation = false,
        Dictionary<string, string>? workingHours = null,
        List<string>? servicesOffered = null)
    {
        if (!new[] { 15, 30, 45, 60 }.Contains(slotDurationMinutes))
            throw new Domain.Exceptions.DomainException("Slot duration must be 15, 30, 45, or 60 minutes.");
        if (advanceBookingDays is < 1 or > 90)
            throw new Domain.Exceptions.DomainException("Advance booking days must be between 1 and 90.");

        SlotDurationMinutes = slotDurationMinutes;
        AdvanceBookingDays = advanceBookingDays;
        CancellationHours = cancellationHours;
        AutoApproveAppointments = autoApproveAppointments;
        RequiresPaymentBeforeConfirmation = requiresPaymentBeforeConfirmation;
        if (workingHours is not null) WorkingHours = workingHours;
        if (servicesOffered is not null) ServicesOffered = servicesOffered;
        UpdateTimestamp();
    }

    public void ApproveTrial()
    {
        SubscriptionStatus = SubscriptionStatus.Trial;
        CurrentPlan = "Trial";
        SubscriptionStartDate = DateOnly.FromDateTime(DateTime.UtcNow);
        SubscriptionEndDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(30));
        RejectionReason = null;
        UpdateTimestamp();
    }

    public void ApproveSubscription(DateOnly startDate, DateOnly endDate)
    {
        SubscriptionStatus = SubscriptionStatus.Active;
        SubscriptionStartDate = startDate;
        SubscriptionEndDate = endDate;
        RejectionReason = null;
        UpdateTimestamp();
    }

    public void ApproveSubscription(string plan, DateOnly startDate, DateOnly endDate)
    {
        CurrentPlan = plan;
        ApproveSubscription(startDate, endDate);
    }

    public void SetSubscriptionPlan(Guid planId)
    {
        SubscriptionPlanId = planId;
        UpdateTimestamp();
    }

    public void MarkPaymentPending(Guid planId, string paymentRef, SubscriptionBillingCycle billingCycle)
    {
        SubscriptionPlanId = planId;
        PendingPaymentRef = paymentRef;
        PendingBillingCycle = billingCycle;
        SubscriptionStatus = SubscriptionStatus.AwaitingActivation;
        UpdateTimestamp();
    }

    public void ActivateFromPayment(string planName, DateOnly startDate, DateOnly endDate)
    {
        if (SubscriptionStatus != SubscriptionStatus.AwaitingActivation)
            throw new DomainException("Center must be in AwaitingActivation status to activate from payment.");
        CurrentPlan = planName;
        SubscriptionStatus = SubscriptionStatus.Active;
        SubscriptionStartDate = startDate;
        SubscriptionEndDate = endDate;
        PendingPaymentRef = null;
        PendingBillingCycle = null;
        RejectionReason = null;
        UpdateTimestamp();
    }

    public void ExpireIfOverdue()
    {
        if (SubscriptionStatus == SubscriptionStatus.Active
            && SubscriptionEndDate.HasValue
            && SubscriptionEndDate.Value < DateOnly.FromDateTime(DateTime.UtcNow))
        {
            SubscriptionStatus = SubscriptionStatus.Expired;
            UpdateTimestamp();
        }
    }

    public void Reject(string reason)
    {
        SubscriptionStatus = SubscriptionStatus.Rejected;
        RejectionReason = reason;
        UpdateTimestamp();
    }

    public void Suspend(string reason)
    {
        SubscriptionStatus = SubscriptionStatus.Suspended;
        UpdateTimestamp();
    }

    public void Reactivate()
    {
        SubscriptionStatus = SubscriptionStatus.Active;
        UpdateTimestamp();
    }

    public void SoftDelete()
    {
        IsDeleted = true;
        DeletedAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void ActivateSubscription(DateOnly startDate, DateOnly endDate)
    {
        SubscriptionStatus = SubscriptionStatus.Active;
        SubscriptionStartDate = startDate;
        SubscriptionEndDate = endDate;
        UpdateTimestamp();
    }

    public void SetLocation(decimal latitude, decimal longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        UpdateTimestamp();
    }

    public void SetProfileImageUrl(string? profileImageUrl)
    {
        ProfileImageUrl = profileImageUrl;
        UpdateTimestamp();
    }

    public void UpdateBranding(string? logoUrl, string? coverUrl, string? description)
    {
        ProfileImageUrl = logoUrl;
        CoverImageUrl = coverUrl;
        if (description is not null) Description = description;
        UpdateTimestamp();
    }

    public bool IsSubscriptionActive =>
        SubscriptionStatus == SubscriptionStatus.Trial ||
        (SubscriptionStatus == SubscriptionStatus.Active &&
         SubscriptionEndDate >= DateOnly.FromDateTime(DateTime.UtcNow));
}

// ─── Subscription History ────────────────────────────────────────────────────

/// <summary>Append-only log of subscription state transitions for a center.</summary>
public class SubscriptionHistory : BaseEntity
{
    public Guid CenterId { get; private set; }
    public SubscriptionStatus OldStatus { get; private set; }
    public SubscriptionStatus NewStatus { get; private set; }
    public string? Plan { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public string? Notes { get; private set; }
    public Guid ChangedBy { get; private set; }
    public DateTime ChangedAt { get; private set; }

    public HealthcareCenter Center { get; private set; } = null!;

    private SubscriptionHistory() { }

    public static SubscriptionHistory Create(
        Guid centerId,
        SubscriptionStatus oldStatus,
        SubscriptionStatus newStatus,
        Guid changedBy,
        string? plan = null,
        DateOnly? startDate = null,
        DateOnly? endDate = null,
        string? notes = null)
    {
        return new SubscriptionHistory
        {
            CenterId = centerId,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            ChangedBy = changedBy,
            Plan = plan,
            StartDate = startDate,
            EndDate = endDate,
            Notes = notes,
            ChangedAt = DateTime.UtcNow
        };
    }
}

// ─── Junction: Doctor ↔ HealthcareCenter ─────────────────────────────────────

/// <summary>
/// Maps to `doctor_healthcare_centers` junction table.
/// A doctor can work at multiple centers; each affiliation has its own consultation fee.
/// </summary>
public class DoctorHealthcareCenter : BaseEntity
{
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }
    public decimal ConsultationFee { get; private set; }
    public DateOnly JoinedDate { get; private set; }
    public bool IsActive { get; private set; } = true;

    // Navigation
    public Doctor Doctor { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;

    private DoctorHealthcareCenter() { }

    public DoctorHealthcareCenter(Guid doctorId, Guid centerId, decimal consultationFee)
    {
        if (consultationFee <= 0)
            throw new Domain.Exceptions.DomainException("Consultation fee must be greater than 0 ETB.");

        DoctorId = doctorId;
        CenterId = centerId;
        ConsultationFee = consultationFee;
        JoinedDate = DateOnly.FromDateTime(DateTime.UtcNow);
    }

    public void UpdateFee(decimal newFee)
    {
        if (newFee <= 0)
            throw new Domain.Exceptions.DomainException("Consultation fee must be greater than 0 ETB.");
        ConsultationFee = newFee;
        UpdateTimestamp();
    }

    public void Deactivate()
    {
        IsActive = false;
        UpdateTimestamp();
    }
}
