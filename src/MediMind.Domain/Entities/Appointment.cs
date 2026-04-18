using MediMind.Domain.Common;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using MediMind.Domain.Events;
using MediMind.Domain.Exceptions;

namespace MediMind.Domain.Entities;

/// <summary>
/// Maps to `appointments` table — the CORE transaction entity.
/// Appointment is the aggregate root. Queue is COMPOSED inside it (cannot exist without appointment).
/// Business rules enforced here, not in application services.
/// </summary>
public class Appointment : BaseEntity
{
    public Guid AppointmentId => Id;
    public Guid PatientId { get; private set; }
    public Guid DoctorId { get; private set; }
    public Guid CenterId { get; private set; }     // == tenant_id
    public DateOnly AppointmentDate { get; private set; }
    public TimeOnly AppointmentTime { get; private set; }
    public int DurationMinutes { get; private set; } = 30;
    public AppointmentStatus Status { get; private set; } = AppointmentStatus.Pending;
    public string ReasonForVisit { get; private set; } = string.Empty;
    public string? Symptoms { get; private set; }
    public DateTime BookingDate { get; private set; } = DateTime.UtcNow;

    // Approval
    public Guid? ApprovedBy { get; private set; }
    public DateTime? ApprovedAt { get; private set; }

    // Cancellation
    public string? CancellationReason { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTime? CancelledAt { get; private set; }

    // Check-in / Check-out
    public DateTime? CheckInTime { get; private set; }
    public DateTime? CheckOutTime { get; private set; }
    public string? Notes { get; private set; }
    public int RescheduleCount { get; private set; }
    public Guid? OriginalAppointmentId { get; private set; }
    public DateTime? Reminder24hSentAt { get; private set; }
    public DateTime? Reminder2hSentAt { get; private set; }

    // Navigation
    public Patient Patient { get; private set; } = null!;
    public Doctor Doctor { get; private set; } = null!;
    public HealthcareCenter Center { get; private set; } = null!;
    public HealthcareCenterAdmin? ApprovedByAdmin { get; private set; }
    public QueueEntry? QueueEntry { get; private set; }           // Composition 1:1
    public VideoConsultation? VideoConsultation { get; private set; }  // Optional 1:0..1
    public ICollection<Prescription> Prescriptions { get; private set; } = [];
    public ICollection<Payment> Payments { get; private set; } = [];

    private Appointment() { }

    /// <summary>
    /// Factory method enforces ALL booking business rules before creating an appointment.
    /// </summary>
    public static Appointment Book(
        Guid patientId,
        Guid doctorId,
        Guid centerId,
        DateOnly appointmentDate,
        TimeOnly appointmentTime,
        int durationMinutes,
        string reasonForVisit,
        string? symptoms,
        int advanceBookingDays,
        int minimumHoursNotice)
    {
        // Rule: Must be at least minimumHoursNotice hours in the future
        var appointmentDateTime = appointmentDate.ToDateTime(appointmentTime);
        var minimumAllowedDateTime = DateTime.UtcNow.AddHours(minimumHoursNotice);

        if (appointmentDateTime < minimumAllowedDateTime)
            throw new DomainException($"Appointments require at least {minimumHoursNotice} hours advance notice.");

        // Rule: Cannot book more than advanceBookingDays in the future
        if (appointmentDate > DateOnly.FromDateTime(DateTime.UtcNow.AddDays(advanceBookingDays)))
            throw new DomainException($"Cannot book appointments more than {advanceBookingDays} days in advance.");

        if (!new[] { 15, 30, 45, 60 }.Contains(durationMinutes))
            durationMinutes = 30; // Default to 30 minutes

        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = doctorId,
            CenterId = centerId,
            AppointmentDate = appointmentDate,
            AppointmentTime = appointmentTime,
            DurationMinutes = durationMinutes,
            ReasonForVisit = reasonForVisit,
            Symptoms = symptoms,
            Status = AppointmentStatus.Pending
        };

        appointment.AddDomainEvent(new AppointmentBookedEvent(appointment.Id, patientId, doctorId, centerId));
        return appointment;
    }

    public void Approve(Guid adminId)
    {
        if (Status != AppointmentStatus.Pending)
            throw new DomainException("Only pending appointments can be approved.");

        Status = AppointmentStatus.Confirmed;
        ApprovedBy = adminId;
        ApprovedAt = DateTime.UtcNow;
        UpdateTimestamp();

        AddDomainEvent(new AppointmentApprovedEvent(Id, PatientId, DoctorId));
    }

    public void Reject(Guid adminId, string reason)
    {
        if (Status != AppointmentStatus.Pending)
            throw new DomainException("Only pending appointments can be rejected.");

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason;
        CancelledBy = adminId;
        CancelledAt = DateTime.UtcNow;
        UpdateTimestamp();

        AddDomainEvent(new AppointmentRejectedEvent(Id, PatientId, reason));
    }

    public void Cancel(Guid cancelledBy, string reason, int minimumHoursNotice)
    {
        if (Status == AppointmentStatus.Cancelled)
            throw new DomainException("Appointment is already cancelled.");

        if (Status == AppointmentStatus.Completed)
            throw new DomainException("Cannot cancel a completed appointment.");

        // Rule: Cannot cancel within minimumHoursNotice of appointment time
        var appointmentDateTime = AppointmentDate.ToDateTime(AppointmentTime);
        if (Status == AppointmentStatus.Confirmed &&
            appointmentDateTime < DateTime.UtcNow.AddHours(minimumHoursNotice))
        {
            throw new DomainException($"Cannot cancel within {minimumHoursNotice} hours of appointment. Contact healthcare center.");
        }

        Status = AppointmentStatus.Cancelled;
        CancellationReason = reason;
        CancelledBy = cancelledBy;
        CancelledAt = DateTime.UtcNow;
        UpdateTimestamp();

        AddDomainEvent(new AppointmentCancelledEvent(Id, PatientId, DoctorId, reason));
    }

    public void MarkInProgress()
    {
        if (Status != AppointmentStatus.Confirmed)
            throw new DomainException("Appointment must be confirmed before marking in-progress.");

        Status = AppointmentStatus.InProgress;
        CheckInTime = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void MarkCompleted()
    {
        if (Status != AppointmentStatus.InProgress)
            throw new DomainException("Appointment must be in-progress to mark as completed.");

        Status = AppointmentStatus.Completed;
        CheckOutTime = DateTime.UtcNow;
        UpdateTimestamp();

        AddDomainEvent(new AppointmentCompletedEvent(Id, PatientId, DoctorId));
    }

    public void MarkNoShow()
    {
        if (Status is not AppointmentStatus.Confirmed and not AppointmentStatus.InProgress)
            throw new DomainException("Only confirmed appointments can be marked as no-show.");

        Status = AppointmentStatus.NoShow;
        UpdateTimestamp();
    }

    public void AddNotes(string notes)
    {
        Notes = notes;
        UpdateTimestamp();
    }

    // ─── Computed Properties ──────────────────────────────────────────────────

    public bool IsCancellable(int minimumHoursNotice) =>
        Status is AppointmentStatus.Pending or AppointmentStatus.Confirmed &&
        AppointmentDate.ToDateTime(AppointmentTime) > DateTime.UtcNow.AddHours(minimumHoursNotice);

    public bool IsFuture => AppointmentDate.ToDateTime(AppointmentTime) > DateTime.UtcNow;

    public bool CanReschedule => Status == AppointmentStatus.Confirmed && RescheduleCount < 1;

    public void IncrementRescheduleCount()
    {
        RescheduleCount++;
        UpdateTimestamp();
    }

    public void LinkToOriginal(Guid originalAppointmentId)
    {
        OriginalAppointmentId = originalAppointmentId;
        UpdateTimestamp();
    }

    public void MarkReminderSent(ReminderType reminderType)
    {
        if (reminderType == ReminderType.TwentyFourHours)
            Reminder24hSentAt = DateTime.UtcNow;
        else
            Reminder2hSentAt = DateTime.UtcNow;
        UpdateTimestamp();
    }

    public void UpdateStatus(AppointmentStatus status, Guid updatedBy)
    {
        switch (status)
        {
            case AppointmentStatus.Confirmed:
                Approve(updatedBy);
                break;
            case AppointmentStatus.Cancelled:
                Cancel(updatedBy, "Status updated", 0);
                break;
            case AppointmentStatus.InProgress:
                MarkInProgress();
                break;
            case AppointmentStatus.Completed:
                MarkCompleted();
                break;
            case AppointmentStatus.NoShow:
                MarkNoShow();
                break;
            default:
                Status = status;
                UpdateTimestamp();
                break;
        }
    }
}
