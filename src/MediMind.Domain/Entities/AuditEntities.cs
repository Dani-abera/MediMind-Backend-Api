using MediMind.Domain.Common;

namespace MediMind.Domain.Entities;

/// <summary>
/// Append-only audit trail entry (NFR-008).
/// No UPDATE or DELETE allowed — enforced at repository level.
/// </summary>
public class AuditLog : BaseEntity
{
    public Guid LogId => Id;
    public DateTime Timestamp { get; private set; }
    public Guid? UserId { get; private set; }
    public string UserType { get; private set; } = string.Empty;
    public Guid? CenterId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string IpAddress { get; private set; } = string.Empty;
    public string UserAgent { get; private set; } = string.Empty;
    public string? Metadata { get; private set; }

    private AuditLog() { }

    public static AuditLog Create(
        string action,
        Guid? userId,
        string userType,
        Guid? centerId,
        string? entityType,
        Guid? entityId,
        string ipAddress,
        string userAgent,
        string? metadata = null)
    {
        return new AuditLog
        {
            Timestamp = DateTime.UtcNow,
            UserId = userId,
            UserType = userType,
            CenterId = centerId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Metadata = metadata
        };
    }
}

public static class AuditActions
{
    public const string LoginSuccess = "LOGIN_SUCCESS";
    public const string LoginFailure = "LOGIN_FAILURE";
    public const string AppointmentCreated = "APPOINTMENT_CREATED";
    public const string AppointmentApproved = "APPOINTMENT_APPROVED";
    public const string AppointmentRejected = "APPOINTMENT_REJECTED";
    public const string AppointmentCancelled = "APPOINTMENT_CANCELLED";
    public const string AppointmentRescheduled = "APPOINTMENT_RESCHEDULED";
    public const string PrescriptionIssued = "PRESCRIPTION_ISSUED";
    public const string PrescriptionRevoked = "PRESCRIPTION_REVOKED";
    public const string PrescriptionDispensed = "PRESCRIPTION_DISPENSED";
    public const string HealthRecordViewed = "HEALTH_RECORD_VIEWED";
    public const string CenterConfigChanged = "CENTER_CONFIG_CHANGED";
    public const string AdminAdded = "ADMIN_ADDED";
    public const string AdminRemoved = "ADMIN_REMOVED";

    // SuperAdmin actions
    public const string SuperAdmin_CenterApproved = "SA_CENTER_APPROVED";
    public const string SuperAdmin_CenterRejected = "SA_CENTER_REJECTED";
    public const string SuperAdmin_CenterSuspended = "SA_CENTER_SUSPENDED";
    public const string SuperAdmin_CenterReactivated = "SA_CENTER_REACTIVATED";
    public const string SuperAdmin_CenterDeleted = "SA_CENTER_DELETED";
    public const string SuperAdmin_SubscriptionUpdated = "SA_SUBSCRIPTION_UPDATED";
    public const string SuperAdmin_SubscriptionExtended = "SA_SUBSCRIPTION_EXTENDED";
    public const string SuperAdmin_DoctorLicenseVerified = "SA_DOCTOR_LICENSE_VERIFIED";
    public const string SuperAdmin_DoctorLicenseRevoked = "SA_DOCTOR_LICENSE_REVOKED";
    public const string SuperAdmin_UserSuspended = "SA_USER_SUSPENDED";
    public const string SuperAdmin_UserReactivated = "SA_USER_REACTIVATED";
    public const string SuperAdmin_UserForceLogout = "SA_USER_FORCE_LOGOUT";
    public const string SuperAdmin_UserDeleted = "SA_USER_DELETED";
    public const string SuperAdmin_QueueRegenerated = "SA_QUEUE_REGENERATED";
}
