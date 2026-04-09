using MediMind.Domain.Common;

namespace MediMind.Domain.Events;

// ─── User Events ──────────────────────────────────────────────────────────────
public record UserActivatedEvent(Guid UserId) : IDomainEvent;

// ─── Appointment Events ───────────────────────────────────────────────────────
public record AppointmentBookedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid CenterId) : IDomainEvent;

public record AppointmentApprovedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId) : IDomainEvent;

public record AppointmentRejectedEvent(
    Guid AppointmentId,
    Guid PatientId,
    string Reason) : IDomainEvent;

public record AppointmentCancelledEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    string Reason) : IDomainEvent;

public record AppointmentCompletedEvent(
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId) : IDomainEvent;

// ─── Queue Events ─────────────────────────────────────────────────────────────
public record PatientCalledEvent(Guid QueueEntryId, Guid PatientId, Guid CenterId, int Position) : IDomainEvent;
public record QueueUpdatedEvent(Guid CenterId, DateOnly QueueDate) : IDomainEvent;

// ─── Health Events ────────────────────────────────────────────────────────────
public record HealthPredictionGeneratedEvent(Guid PredictionId, Guid PatientId) : IDomainEvent;
