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
    public DbSet<Payment> Payments => Set<Payment>();

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
            // Only rename keys on hierarchy roots — TPT shares one key across types; touching it per entity would be redundant.
            if (entity.BaseType is null)
            {
                foreach (var key in entity.GetKeys())
                {
                    var keyName = key.GetName();
                    if (keyName is not null)
                        key.SetName(ToSnakeCase(keyName));
                }
            }
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
            await mediator.Publish(domainEvent, ct);

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
