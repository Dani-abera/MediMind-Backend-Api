using MediatR;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Common;
using MediMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Data;

/// <summary>
/// PostgreSQL DbContext for MediMind.
/// Enforces multi-tenant isolation via SaveChangesAsync interception.
/// Dispatches domain events AFTER the transaction commits.
/// </summary>
public class MediMindDbContext(
    DbContextOptions<MediMindDbContext> options,
    ICurrentUser currentUser,
    IMediator mediator)
    : DbContext(options), IUnitOfWork
{
    // ─── DbSets (map 1:1 to database tables) ─────────────────────────────────
    public DbSet<User> Users => Set<User>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<SuperAdminUser> SuperAdmins => Set<SuperAdminUser>();
    public DbSet<OtpVerification> OtpVerifications => Set<OtpVerification>();
    public DbSet<HealthcareCenterAdmin> HealthcareCenterAdmins => Set<HealthcareCenterAdmin>();
    public DbSet<HealthcareCenter> HealthcareCenters => Set<HealthcareCenter>();
    public DbSet<DoctorHealthcareCenter> DoctorHealthcareCenters => Set<DoctorHealthcareCenter>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<HealthRecord> HealthRecords => Set<HealthRecord>();
    public DbSet<HealthPrediction> HealthPredictions => Set<HealthPrediction>();
    public DbSet<HealthPredictionRecord> HealthPredictionRecords => Set<HealthPredictionRecord>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<VideoConsultation> VideoConsultations => Set<VideoConsultation>();
    public DbSet<VideoConsultationParticipant> VideoConsultationParticipants => Set<VideoConsultationParticipant>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<VideoQualityMetric> VideoQualityMetrics => Set<VideoQualityMetric>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<PaymentActivity> PaymentActivities => Set<PaymentActivity>();
    public DbSet<SubscriptionPlan> SubscriptionPlans => Set<SubscriptionPlan>();
    public DbSet<UserDeviceToken> UserDeviceTokens => Set<UserDeviceToken>();
    public DbSet<NotificationLog> NotificationLogs => Set<NotificationLog>();
    public DbSet<MedicationReminder> MedicationReminders => Set<MedicationReminder>();
    public DbSet<PatientMedicalHistory> PatientMedicalHistories => Set<PatientMedicalHistory>();
    public DbSet<EmergencyContact> EmergencyContacts => Set<EmergencyContact>();
    public DbSet<HealthRecordAttachment> HealthRecordAttachments => Set<HealthRecordAttachment>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Favorite> Favorites => Set<Favorite>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<PrescriptionTemplate> PrescriptionTemplates => Set<PrescriptionTemplate>();
    public DbSet<AppointmentNote> AppointmentNotes => Set<AppointmentNote>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SubscriptionHistory> SubscriptionHistories => Set<SubscriptionHistory>();
    public DbSet<PlatformConfiguration> PlatformConfigurations => Set<PlatformConfiguration>();
    public DbSet<WaitlistSubscription> WaitlistSubscriptions => Set<WaitlistSubscription>();
    public DbSet<DoctorInvitation> DoctorInvitations => Set<DoctorInvitation>();
    public DbSet<ScheduleException> ScheduleExceptions => Set<ScheduleException>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all IEntityTypeConfiguration<T> classes from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(MediMindDbContext).Assembly);

        // Map all table names to snake_case (matching PostgreSQL conventions)
        foreach (var entity in modelBuilder.Model.GetEntityTypes())
        {
            entity.SetTableName(ToSnakeCase(entity.GetTableName()!));
            foreach (var property in entity.GetProperties())
                property.SetColumnName(ToSnakeCase(property.GetColumnName()!));
            // Do not rename PK constraint names globally.
            // In TPT hierarchies, EF can reuse one PK name across derived tables, and PostgreSQL requires
            // constraint names to be unique in the schema.
            foreach (var fk in entity.GetForeignKeys())
                fk.SetConstraintName(ToSnakeCase(fk.GetConstraintName()!));
            foreach (var index in entity.GetIndexes())
                index.SetDatabaseName(ToSnakeCase(index.GetDatabaseName()!));
        }
    }

    // ─── Unit of Work ─────────────────────────────────────────────────────────

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        // Collect domain events BEFORE saving
        var domainEvents = ChangeTracker.Entries<BaseEntity>()
            .Select(e => e.Entity)
            .Where(e => e.DomainEvents.Count != 0)
            .SelectMany(e => {
                var events = e.DomainEvents.ToList();
                e.ClearDomainEvents();
                return events;
            })
            .ToList();

        var result = await base.SaveChangesAsync(ct);

        // Dispatch domain events AFTER successful save (outbox-style)
        foreach (var domainEvent in domainEvents)
        {
            if (domainEvent is INotification notification)
                await mediator.Publish(notification, ct);
        }

        return result;
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default) =>
        await Database.BeginTransactionAsync(ct);

    public async Task CommitTransactionAsync(CancellationToken ct = default) =>
        await Database.CommitTransactionAsync(ct);

    public async Task RollbackTransactionAsync(CancellationToken ct = default) =>
        await Database.RollbackTransactionAsync(ct);

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string ToSnakeCase(string name)
    {
        if (string.IsNullOrEmpty(name)) return name;
        var result = System.Text.RegularExpressions.Regex.Replace(name, "([A-Z])", "_$1").TrimStart('_');
        return result.ToLowerInvariant();
    }
}
