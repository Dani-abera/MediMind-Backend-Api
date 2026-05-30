using System.Text.Json;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace MediMind.Infrastructure.Data.Configurations;

/// <summary>
/// JSON conversions so EF InMemory/SQLite can persist collection properties (PostgreSQL text[]/jsonb alone is not enough).
/// </summary>
file static class JsonCollectionMapping
{
    private static readonly JsonSerializerOptions Json = new();

    private static readonly ValueConverter<List<string>, string> StringListConverter = new(
        v => JsonSerializer.Serialize(v, Json),
        v => string.IsNullOrWhiteSpace(v)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(v, Json) ?? new List<string>());

    private static readonly ValueComparer<List<string>> StringListComparer = new(
        (a, b) => (a ?? new List<string>()).SequenceEqual(b ?? new List<string>()),
        v => v.Aggregate(0, (h, s) => HashCode.Combine(h, s.GetHashCode(StringComparison.Ordinal))),
        v => new List<string>(v ?? new List<string>()));

    private static readonly ValueConverter<Dictionary<string, string>, string> StringDictConverter = new(
        v => JsonSerializer.Serialize(v, Json),
        v => string.IsNullOrWhiteSpace(v)
            ? new Dictionary<string, string>()
            : JsonSerializer.Deserialize<Dictionary<string, string>>(v, Json) ?? new Dictionary<string, string>());

    private static readonly ValueComparer<Dictionary<string, string>> StringDictComparer = new(
        (a, b) => JsonSerializer.Serialize(a ?? new Dictionary<string, string>(), Json) ==
                  JsonSerializer.Serialize(b ?? new Dictionary<string, string>(), Json),
        v => JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), Json).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<Dictionary<string, string>>(
                 JsonSerializer.Serialize(v ?? new Dictionary<string, string>(), Json), Json) ??
             new Dictionary<string, string>());

    public static void MapStringList(PropertyBuilder<List<string>> property, string columnType)
    {
        property.HasConversion(StringListConverter).HasColumnType(columnType);
        property.Metadata.SetValueComparer(StringListComparer);
    }

    public static void MapStringDictionary(PropertyBuilder<Dictionary<string, string>> property, string columnType)
    {
        property.HasConversion(StringDictConverter).HasColumnType(columnType);
        property.Metadata.SetValueComparer(StringDictComparer);
    }

    private static readonly ValueConverter<List<MedicationItem>, string> MedicationListConverter = new(
        v => JsonSerializer.Serialize(v, Json),
        v => string.IsNullOrWhiteSpace(v)
            ? new List<MedicationItem>()
            : JsonSerializer.Deserialize<List<MedicationItem>>(v, Json) ?? new List<MedicationItem>());

    private static readonly ValueComparer<List<MedicationItem>> MedicationListComparer = new(
        (a, b) => JsonSerializer.Serialize(a ?? new List<MedicationItem>(), Json) ==
                  JsonSerializer.Serialize(b ?? new List<MedicationItem>(), Json),
        v => JsonSerializer.Serialize(v ?? new List<MedicationItem>(), Json).GetHashCode(StringComparison.Ordinal),
        v => JsonSerializer.Deserialize<List<MedicationItem>>(
                 JsonSerializer.Serialize(v ?? new List<MedicationItem>(), Json), Json) ??
             new List<MedicationItem>());

    public static void MapMedicationItems(PropertyBuilder<List<MedicationItem>> property, string columnType)
    {
        property.HasConversion(MedicationListConverter).HasColumnType(columnType);
        property.Metadata.SetValueComparer(MedicationListComparer);
    }
}

// ─── User ─────────────────────────────────────────────────────────────────────

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        // Do not call HasName on the TPT root — EF applies one PK name to every table in the hierarchy,
        // which breaks PostgreSQL (constraint names must be unique per schema). Per-table names come from each ToTable.
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("user_id");
        builder.Property(u => u.Email).HasMaxLength(255).IsRequired();
        builder.Property(u => u.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(u => u.FullName).HasMaxLength(100).IsRequired();
        builder.Property(u => u.PasswordHash).HasMaxLength(255).IsRequired();
        builder.Property(u => u.ProfileImageUrl).HasMaxLength(1024);
        builder.Property(u => u.UserType).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(u => u.Gender).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(u => u.Email).IsUnique();
        builder.HasIndex(u => new { u.PhoneNumber, u.UserType }).IsUnique();
        builder.HasIndex(u => new { u.UserType, u.Status });

        // TPT (Table-Per-Type) inheritance
        builder.UseTptMappingStrategy();
    }
}

public class PatientConfiguration : IEntityTypeConfiguration<Patient>
{
    public void Configure(EntityTypeBuilder<Patient> builder)
    {
        builder.ToTable("patients");
        builder.Property(p => p.Id).HasColumnName("patient_id");
        builder.Property(p => p.Address).HasMaxLength(300);
        builder.Property(p => p.BloodType).HasConversion<string>().HasMaxLength(20);
        JsonCollectionMapping.MapStringList(builder.Property(p => p.ChronicConditions), "text");
        JsonCollectionMapping.MapStringList(builder.Property(p => p.CurrentMedications), "text");

        // Navigation
        builder.HasMany(p => p.HealthRecords).WithOne(h => h.Patient)
            .HasForeignKey(h => h.PatientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.HealthPredictions).WithOne(h => h.Patient)
            .HasForeignKey(h => h.PatientId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(p => p.Appointments).WithOne(a => a.Patient)
            .HasForeignKey(a => a.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.Payments).WithOne(p => p.Patient)
            .HasForeignKey(p => p.PatientId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(p => p.MedicationReminders).WithOne(r => r.Patient)
            .HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Cascade);

        builder.Ignore(p => p.EnrolledCenters);
    }
}

public class DoctorConfiguration : IEntityTypeConfiguration<Doctor>
{
    public void Configure(EntityTypeBuilder<Doctor> builder)
    {
        builder.ToTable("doctors");
        builder.Property(d => d.Id).HasColumnName("doctor_id");
        builder.Property(d => d.BadgeNumber).HasMaxLength(6).IsRequired();
        builder.Property(d => d.Specialization).HasMaxLength(100).IsRequired();
        builder.Property(d => d.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(d => d.Biography).HasMaxLength(2000);
        JsonCollectionMapping.MapStringList(builder.Property(d => d.LanguagesSpoken), "text");

        builder.HasIndex(d => d.LicenseNumber).IsUnique();
        builder.HasIndex(d => d.BadgeNumber).IsUnique();
        builder.HasIndex(d => d.Specialization);

        builder.HasMany(d => d.Appointments).WithOne(a => a.Doctor)
            .HasForeignKey(a => a.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Prescriptions).WithOne(p => p.Doctor)
            .HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(d => d.Schedules).WithOne(s => s.Doctor)
            .HasForeignKey(s => s.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class OtpVerificationConfiguration : IEntityTypeConfiguration<OtpVerification>
{
    public void Configure(EntityTypeBuilder<OtpVerification> builder)
    {
        builder.ToTable("otp_verifications");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Code).HasMaxLength(6).IsRequired();
        builder.Property(x => x.Purpose).HasMaxLength(50).IsRequired();
        builder.Property(x => x.ExpirationTime).IsRequired();
        builder.Property(x => x.IsUsed).HasDefaultValue(false);
        builder.HasIndex(x => new { x.PhoneNumber, x.Purpose, x.IsUsed });
    }
}

// ─── Healthcare Center ────────────────────────────────────────────────────────

public class HealthcareCenterConfiguration : IEntityTypeConfiguration<HealthcareCenter>
{
    public void Configure(EntityTypeBuilder<HealthcareCenter> builder)
    {
        builder.ToTable("healthcare_centers");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("center_id");
        builder.Property(c => c.CenterName).HasMaxLength(200).IsRequired();
        builder.Property(c => c.CenterType).HasMaxLength(50).IsRequired();
        builder.Property(c => c.LicenseNumber).HasMaxLength(50).IsRequired();
        builder.Property(c => c.Address).HasMaxLength(500).IsRequired();
        builder.Property(c => c.City).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Region).HasMaxLength(100).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.Email).HasMaxLength(255).IsRequired();

        // JSON columns (serialized for InMemory/SQLite; jsonb when using PostgreSQL)
        JsonCollectionMapping.MapStringDictionary(builder.Property(c => c.WorkingHours), "jsonb");
        builder.Property(c => c.WorkingHours).IsRequired();
        JsonCollectionMapping.MapStringList(builder.Property(c => c.ServicesOffered), "text");
        builder.Property(c => c.ServicesOffered).IsRequired();
        JsonCollectionMapping.MapStringList(builder.Property(c => c.Specializations), "text");

        builder.Property(c => c.SubscriptionStatus).HasConversion<string>().HasMaxLength(30);
        builder.Property(c => c.PendingPaymentRef).HasMaxLength(100);
        builder.Property(c => c.PendingBillingCycle).HasConversion<int?>().IsRequired(false);
        builder.Property(c => c.Latitude).HasPrecision(10, 8);
        builder.Property(c => c.Longitude).HasPrecision(11, 8);
        builder.Property(c => c.ProfileImageUrl).HasMaxLength(1024);

        // Business rule: slot duration must be 15, 30, 45, or 60
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_slot_duration", "slot_duration_minutes IN (15, 30, 45, 60)"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_advance_booking", "advance_booking_days BETWEEN 1 AND 90"));

        builder.Property(c => c.RequiresPaymentBeforeConfirmation).HasDefaultValue(false);

        builder.HasIndex(c => c.LicenseNumber).IsUnique();
        builder.HasIndex(c => new { c.City, c.Region });
        builder.HasIndex(c => c.SubscriptionStatus);

        builder.HasMany(c => c.Admins).WithOne(a => a.Center)
            .HasForeignKey(a => a.CenterId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.Appointments).WithOne(a => a.Center)
            .HasForeignKey(a => a.CenterId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(c => c.QueueEntries).WithOne(q => q.Center)
            .HasForeignKey(q => q.CenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class DoctorHealthcareCenterConfiguration : IEntityTypeConfiguration<DoctorHealthcareCenter>
{
    public void Configure(EntityTypeBuilder<DoctorHealthcareCenter> builder)
    {
        builder.ToTable("doctor_healthcare_centers");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.ConsultationFee).HasPrecision(10, 2).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_consultation_fee", "consultation_fee > 0"));

        // Prevent duplicate doctor-center affiliations
        builder.HasIndex(d => new { d.DoctorId, d.CenterId }).IsUnique();

        builder.HasOne(d => d.Doctor).WithMany(doc => doc.DoctorHealthcareCenters)
            .HasForeignKey(d => d.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(d => d.Center).WithMany(c => c.DoctorHealthcareCenters)
            .HasForeignKey(d => d.CenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

public class SuperAdminUserConfiguration : IEntityTypeConfiguration<SuperAdminUser>
{
    public void Configure(EntityTypeBuilder<SuperAdminUser> builder)
    {
        builder.ToTable("super_admins");
        builder.Property(s => s.Id).HasColumnName("super_admin_id");
    }
}

// ─── Waitlist Subscription ────────────────────────────────────────────────────

public class WaitlistSubscriptionConfiguration : IEntityTypeConfiguration<WaitlistSubscription>
{
    public void Configure(EntityTypeBuilder<WaitlistSubscription> builder)
    {
        builder.ToTable("waitlist_subscriptions");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.IsActive).HasDefaultValue(true);
        builder.HasIndex(w => new { w.PatientId, w.DoctorId, w.CenterId });
        builder.HasIndex(w => new { w.DoctorId, w.CenterId, w.IsActive });

        builder.HasOne(w => w.Patient).WithMany()
            .HasForeignKey(w => w.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Doctor).WithMany()
            .HasForeignKey(w => w.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(w => w.Center).WithMany()
            .HasForeignKey(w => w.CenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

// ─── Doctor Schedule ──────────────────────────────────────────────────────────

public class DoctorScheduleConfiguration : IEntityTypeConfiguration<DoctorSchedule>
{
    public void Configure(EntityTypeBuilder<DoctorSchedule> builder)
    {
        builder.ToTable("doctor_schedules");
        builder.HasKey(s => s.Id);
        JsonCollectionMapping.MapStringList(builder.Property(s => s.WorkingDays), "text");
        builder.Property(s => s.WorkingDays).IsRequired();
        builder.Property(s => s.SlotDuration).IsRequired();
        builder.ToTable(t => t.HasCheckConstraint("ck_schedule_times", "end_time > start_time"));
        builder.ToTable(t => t.HasCheckConstraint("ck_break_times", "break_end > break_start OR break_start IS NULL"));

        // One schedule per doctor per center
        builder.HasIndex(s => new { s.DoctorId, s.CenterId }).IsUnique();
    }
}

// ─── Doctor Invitation ────────────────────────────────────────────────────────

public class DoctorInvitationConfiguration : IEntityTypeConfiguration<DoctorInvitation>
{
    public void Configure(EntityTypeBuilder<DoctorInvitation> builder)
    {
        builder.ToTable("doctor_invitations");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Email).IsRequired().HasMaxLength(200);
        builder.Property(i => i.FullName).IsRequired().HasMaxLength(100);
        builder.Property(i => i.LicenseNumber).IsRequired().HasMaxLength(100);
        builder.Property(i => i.Specialization).IsRequired().HasMaxLength(100);
        builder.Property(i => i.PhoneNumber).IsRequired().HasMaxLength(20);
        builder.Property(i => i.Token).IsRequired().HasMaxLength(64);
        builder.Property(i => i.ConsultationFee).HasColumnType("decimal(10,2)");
        builder.HasIndex(i => i.Token).IsUnique();
        builder.HasIndex(i => new { i.Email, i.CenterId });
    }
}

// ─── Schedule Exception ───────────────────────────────────────────────────────

public class ScheduleExceptionConfiguration : IEntityTypeConfiguration<ScheduleException>
{
    public void Configure(EntityTypeBuilder<ScheduleException> builder)
    {
        builder.ToTable("schedule_exceptions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Reason).IsRequired().HasMaxLength(200);
        builder.HasIndex(e => new { e.DoctorId, e.CenterId, e.ExceptionDate }).IsUnique();
    }
}

// ─── Appointment ─────────────────────────────────────────────────────────────

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("appointment_id");
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.AppointmentType).HasConversion<string>().HasMaxLength(20).HasDefaultValue(AppointmentType.InPerson);
        builder.Property(a => a.ReasonForVisit).IsRequired();
        builder.Property(a => a.DurationMinutes).HasDefaultValue(30);
        builder.Property(a => a.BookingDate).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.Property(a => a.RescheduleCount).HasDefaultValue(0);
        builder.Property(a => a.Reminder24hSentAt);
        builder.Property(a => a.Reminder2hSentAt);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_duration_minutes", "duration_minutes IN (15, 30, 45, 60)"));
        // Past-date guard applies only to new bookings (status='Pending').
        // Once an appointment is Confirmed/InProgress/Completed/Cancelled/NoShow
        // it must remain writable for normal lifecycle updates (check-in,
        // marking complete after the call, etc.) even though its date is now
        // in the past — otherwise every post-appointment write throws 23514.
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_appointment_date_not_past",
            "status <> 'Pending' OR appointment_date >= CURRENT_DATE"));

        // THE most critical unique constraint: prevent double-booking
        // Partial index: cancelled appointments free up the slot for rebooking
        builder.HasIndex(a => new { a.DoctorId, a.CenterId, a.AppointmentDate, a.AppointmentTime })
            .IsUnique()
            .HasFilter("status <> 'Cancelled'")
            .HasDatabaseName("idx_appointments_no_double_booking");

        builder.HasIndex(a => new { a.PatientId, a.AppointmentDate });
        builder.HasIndex(a => new { a.DoctorId, a.AppointmentDate });
        builder.HasIndex(a => new { a.CenterId, a.AppointmentDate });
        builder.HasIndex(a => a.Status);

        // 1:1 Composition — Queue is deleted when Appointment is deleted
        builder.HasOne(a => a.QueueEntry).WithOne(q => q.Appointment)
            .HasForeignKey<QueueEntry>(q => q.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(a => a.VideoConsultation).WithOne(v => v.Appointment)
            .HasForeignKey<VideoConsultation>(v => v.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Prescriptions).WithOne(p => p.Appointment)
            .HasForeignKey(p => p.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(a => a.Payments).WithOne(p => p.Appointment)
            .HasForeignKey(p => p.AppointmentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
    }
}

// ─── Queue ────────────────────────────────────────────────────────────────────

public class QueueEntryConfiguration : IEntityTypeConfiguration<QueueEntry>
{
    public void Configure(EntityTypeBuilder<QueueEntry> builder)
    {
        builder.ToTable("queue");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).HasColumnName("queue_id");
        builder.Property(q => q.QueueNumber).HasMaxLength(10).IsRequired();
        builder.Property(q => q.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(q => q.RoomNumber).HasMaxLength(50);
        builder.ToTable(t => t.HasCheckConstraint("ck_queue_position", "position > 0"));
        builder.ToTable(t => t.HasCheckConstraint("ck_estimated_wait", "estimated_wait_time_minutes >= 0"));

        // One queue entry per appointment
        builder.HasIndex(q => q.AppointmentId).IsUnique();
        // Prevent duplicate queue numbers per day per center
        builder.HasIndex(q => new { q.CenterId, q.QueueDate, q.QueueNumber }).IsUnique();
        builder.HasIndex(q => new { q.CenterId, q.QueueDate, q.Status });
        builder.HasIndex(q => new { q.CenterId, q.Position });
    }
}

// ─── Health Records ───────────────────────────────────────────────────────────

public class HealthRecordConfiguration : IEntityTypeConfiguration<HealthRecord>
{
    public void Configure(EntityTypeBuilder<HealthRecord> builder)
    {
        builder.ToTable("health_records");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("record_id");
        builder.Property(h => h.RecordDate).IsRequired();
        builder.Property(h => h.RecordTime).HasDefaultValueSql("CURRENT_TIME");
        builder.Property(h => h.GlucoseLevel).HasPrecision(5, 2);
        builder.Property(h => h.Weight).HasPrecision(5, 2);
        builder.Property(h => h.Height).HasPrecision(5, 2);
        builder.Property(h => h.Temperature).HasPrecision(4, 2);
        builder.Property(h => h.RecordedBy).HasMaxLength(50).HasDefaultValue("patient");
        builder.Property(h => h.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");

        // Domain invariants as DB constraints
        builder.ToTable(t => t.HasCheckConstraint("ck_systolic_bp", "systolic_bp BETWEEN 70 AND 250 OR systolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_diastolic_bp", "diastolic_bp BETWEEN 40 AND 150 OR diastolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_bp_relation", "systolic_bp > diastolic_bp OR systolic_bp IS NULL OR diastolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_glucose", "glucose_level BETWEEN 30 AND 600 OR glucose_level IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_weight", "weight BETWEEN 20 AND 300 OR weight IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_height", "height BETWEEN 50 AND 250 OR height IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_temperature", "temperature BETWEEN 35 AND 43 OR temperature IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_heart_rate", "heart_rate BETWEEN 30 AND 250 OR heart_rate IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_oxygen_saturation", "oxygen_saturation BETWEEN 70 AND 100 OR oxygen_saturation IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_respiratory_rate", "respiratory_rate BETWEEN 8 AND 60 OR respiratory_rate IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_recorded_by", "recorded_by IN ('patient', 'doctor')"));

        builder.Ignore(h => h.Bmi); // Computed property — not stored in DB

        builder.HasIndex(h => new { h.PatientId, h.RecordDate })
            .IsDescending(false, true);
    }
}

// ─── Health Prediction ────────────────────────────────────────────────────────

public class HealthPredictionConfiguration : IEntityTypeConfiguration<HealthPrediction>
{
    private static readonly ValueConverter<Dictionary<string, List<string>>, string> ContributingFactorsConverter =
        new(
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => string.IsNullOrWhiteSpace(v)
                ? new Dictionary<string, List<string>>()
                : JsonSerializer.Deserialize<Dictionary<string, List<string>>>(v, (JsonSerializerOptions?)null)
                  ?? new Dictionary<string, List<string>>());

    private static readonly ValueComparer<Dictionary<string, List<string>>> ContributingFactorsComparer =
        new(
            (a, b) => JsonSerializer.Serialize(a, (JsonSerializerOptions?)null) ==
                      JsonSerializer.Serialize(b, (JsonSerializerOptions?)null),
            v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null).GetHashCode(),
            v => JsonSerializer.Deserialize<Dictionary<string, List<string>>>(
                     JsonSerializer.Serialize(v, (JsonSerializerOptions?)null), (JsonSerializerOptions?)null)
                 ?? new Dictionary<string, List<string>>());

    public void Configure(EntityTypeBuilder<HealthPrediction> builder)
    {
        builder.ToTable("health_predictions");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("prediction_id");
        builder.Property(h => h.DiabetesRisk).HasPrecision(5, 2).IsRequired();
        builder.Property(h => h.HypertensionRisk).HasPrecision(5, 2).IsRequired();
        builder.Property(h => h.CvdRisk).HasPrecision(5, 2).IsRequired();
        builder.Property(h => h.Confidence).HasPrecision(5, 2).IsRequired();
        builder.Property(h => h.DiabetesCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.HypertensionCategory).HasConversion<string>().HasMaxLength(20);
        builder.Property(h => h.CvdCategory).HasConversion<string>().HasMaxLength(20);
        var contributingFactors = builder.Property(h => h.ContributingFactors)
            .HasConversion(ContributingFactorsConverter)
            .HasColumnType("jsonb")
            .IsRequired();
        contributingFactors.Metadata.SetValueComparer(ContributingFactorsComparer);
        builder.Property(h => h.ModelVersion).HasMaxLength(20).IsRequired();
        builder.Property(h => h.Recommendations).IsRequired();
        builder.Property(h => h.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.ToTable(t => t.HasCheckConstraint("ck_data_points", "data_points_used > 0"));

        builder.HasIndex(h => new { h.PatientId, h.PredictionDate })
            .IsDescending(false, true);

        builder.HasMany(h => h.PredictionRecords).WithOne(r => r.Prediction)
            .HasForeignKey(r => r.PredictionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HealthPredictionRecordConfiguration : IEntityTypeConfiguration<HealthPredictionRecord>
{
    public void Configure(EntityTypeBuilder<HealthPredictionRecord> builder)
    {
        builder.ToTable("health_prediction_records");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("id");
        builder.HasIndex(r => new { r.PredictionId, r.RecordId }).IsUnique();
        builder.HasOne(r => r.Prediction).WithMany(p => p.PredictionRecords)
            .HasForeignKey(r => r.PredictionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(r => r.Record).WithMany()
            .HasForeignKey(r => r.RecordId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Payment ──────────────────────────────────────────────────────────────────

public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("payments");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("payment_id");
        builder.Property(p => p.PaymentRef).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Amount).HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.BaseAmount).HasPrecision(10, 2);
        builder.Property(p => p.VatPercent).HasPrecision(5, 2);
        builder.Property(p => p.VatFee).HasPrecision(10, 2);
        builder.Property(p => p.ServicePercent).HasPrecision(5, 2);
        builder.Property(p => p.ServiceFee).HasPrecision(10, 2);
        builder.Property(p => p.TotalAmount).HasPrecision(10, 2);
        builder.Property(p => p.Currency).HasMaxLength(10).HasDefaultValue("ETB");
        builder.Property(p => p.PaymentMethod).HasMaxLength(50);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ReasonType).HasConversion<string>().HasMaxLength(30);
        builder.Property(p => p.ChapaTransactionId).HasMaxLength(100);
        builder.Property(p => p.ChapaCheckoutUrl).HasMaxLength(2000);
        builder.Property(p => p.ReceiptUrl).HasMaxLength(2000);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.ToTable(t => t.HasCheckConstraint("ck_payment_amount", "amount > 0"));

        builder.HasIndex(p => p.PaymentRef).IsUnique();
        builder.HasIndex(p => p.ChapaTransactionId)
            .IsUnique()
            .HasFilter("chapa_transaction_id IS NOT NULL");
        builder.Property(p => p.AdminId).IsRequired(false);
        builder.HasIndex(p => p.AppointmentId);
        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.CenterId);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.ReasonType);

        builder.HasMany(p => p.Activities).WithOne(a => a.Payment)
            .HasForeignKey(a => a.PaymentId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class PaymentActivityConfiguration : IEntityTypeConfiguration<PaymentActivity>
{
    public void Configure(EntityTypeBuilder<PaymentActivity> builder)
    {
        builder.ToTable("payment_activities");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("activity_id");
        builder.Property(a => a.Action).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(a => a.ChapaTransactionId).HasMaxLength(100);
        builder.Property(a => a.PaymentRef).HasMaxLength(100);
        builder.Property(a => a.TotalAmount).HasPrecision(10, 2).IsRequired();
        builder.Property(a => a.AmountPaid).HasPrecision(10, 2);
        builder.Property(a => a.ChargePaid).HasPrecision(10, 2);
        builder.Property(a => a.Currency).HasMaxLength(10).HasDefaultValue("ETB");
        builder.Property(a => a.PaymentMethodRaw).HasMaxLength(50);
        builder.Property(a => a.GatewayMessage).HasMaxLength(500);
        builder.Property(a => a.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");

        builder.HasIndex(a => a.PaymentId);
        builder.HasIndex(a => a.CreatedAt);
    }
}

public class SubscriptionPlanConfiguration : IEntityTypeConfiguration<SubscriptionPlan>
{
    public void Configure(EntityTypeBuilder<SubscriptionPlan> builder)
    {
        builder.ToTable("subscription_plans");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("plan_id");
        builder.Property(p => p.Name).HasMaxLength(100).IsRequired();
        builder.Property(p => p.Tier).HasConversion<int>().IsRequired();
        builder.Property(p => p.MonthlyPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.YearlyPrice).HasPrecision(10, 2).IsRequired();
        builder.Property(p => p.MaxDoctors).IsRequired();
        builder.Property(p => p.MaxAppointmentsPerDay).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(1000);
        builder.Property(p => p.IsActive).HasDefaultValue(true);
        builder.Property(p => p.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        JsonCollectionMapping.MapStringList(builder.Property(p => p.Features), "jsonb");

        builder.HasIndex(p => p.Name).IsUnique();
        builder.HasIndex(p => p.Tier);
        builder.HasIndex(p => p.IsActive);
    }
}

// ─── Prescription ─────────────────────────────────────────────────────────────

public class PrescriptionConfiguration : IEntityTypeConfiguration<Prescription>
{
    public void Configure(EntityTypeBuilder<Prescription> builder)
    {
        builder.ToTable("prescriptions");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("prescription_id");
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.PrescriptionUrl).HasMaxLength(1024);
        JsonCollectionMapping.MapMedicationItems(builder.Property(p => p.Medications), "jsonb");
        builder.Property(p => p.Medications).IsRequired();
        JsonCollectionMapping.MapStringList(builder.Property(p => p.LabTests), "text");
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_expiry_date", "expiry_date > issue_date OR expiry_date IS NULL"));

        builder.HasIndex(p => p.AppointmentId).IsUnique()
            .HasFilter("appointment_id IS NOT NULL");

        builder.HasOne(p => p.Appointment).WithMany(a => a.Prescriptions)
            .HasForeignKey(p => p.AppointmentId).IsRequired(false).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Patient).WithMany(pt => pt.Prescriptions)
            .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Doctor).WithMany(d => d.Prescriptions)
            .HasForeignKey(p => p.DoctorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(p => p.Center).WithMany()
            .HasForeignKey(p => p.CenterId).OnDelete(DeleteBehavior.Restrict);
    }
}

// ─── Video Consultation ───────────────────────────────────────────────────────

public class VideoConsultationConfiguration : IEntityTypeConfiguration<VideoConsultation>
{
    public void Configure(EntityTypeBuilder<VideoConsultation> builder)
    {
        builder.ToTable("video_consultations");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).HasColumnName("consultation_id");
        builder.Property(v => v.RoomId).HasMaxLength(100).IsRequired();
        builder.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(v => v.VideoQuality).HasMaxLength(20);
        builder.ToTable(t => t.HasCheckConstraint("ck_duration_minutes_vc", "duration_minutes >= 0 OR duration_minutes IS NULL"));

        // One video call per appointment max
        builder.HasIndex(v => v.AppointmentId).IsUnique();
        builder.HasIndex(v => v.RoomId).IsUnique();

        builder.HasMany(v => v.Participants).WithOne()
            .HasForeignKey(p => p.ConsultationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<ChatMessage>().WithOne(m => m.Consultation)
            .HasForeignKey(m => m.ConsultationId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany<VideoQualityMetric>().WithOne(m => m.Consultation)
            .HasForeignKey(m => m.ConsultationId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class VideoConsultationParticipantConfiguration
    : IEntityTypeConfiguration<VideoConsultationParticipant>
{
    public void Configure(EntityTypeBuilder<VideoConsultationParticipant> builder)
    {
        builder.ToTable("video_consultation_participants");
        builder.HasKey(p => p.Id);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_participant_identity",
            "patient_id IS NOT NULL OR doctor_id IS NOT NULL"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_left_after_joined",
            "left_at > joined_at OR left_at IS NULL"));
    }
}

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("chat_messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("message_id");
        builder.Property(m => m.SenderType).HasMaxLength(20).IsRequired();
        builder.Property(m => m.Content).HasMaxLength(2000).IsRequired();
        builder.Property(m => m.SentAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.Property(m => m.IsRead).HasDefaultValue(false);
        builder.HasIndex(m => new { m.ConsultationId, m.SentAt }).IsDescending(false, true);
    }
}

public class VideoQualityMetricConfiguration : IEntityTypeConfiguration<VideoQualityMetric>
{
    public void Configure(EntityTypeBuilder<VideoQualityMetric> builder)
    {
        builder.ToTable("video_quality_metrics");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.ReportedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.HasIndex(m => new { m.ConsultationId, m.ReportedAt });
    }
}

public class UserDeviceTokenConfiguration : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("user_device_tokens");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("token_id");
        builder.Property(t => t.FcmToken).HasMaxLength(512).IsRequired();
        builder.Property(t => t.DevicePlatform).HasMaxLength(20).IsRequired();
        builder.Property(t => t.DeviceModel).HasMaxLength(200);
        builder.Property(t => t.RegisteredAt).IsRequired();
        builder.Property(t => t.LastUsedAt).IsRequired();
        builder.Property(t => t.IsActive).HasDefaultValue(true);

        builder.HasOne(t => t.User).WithMany()
            .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => new { t.UserId, t.IsActive });
        builder.HasIndex(t => new { t.UserId, t.FcmToken }).IsUnique();
    }
}

public class NotificationLogConfiguration : IEntityTypeConfiguration<NotificationLog>
{
    public void Configure(EntityTypeBuilder<NotificationLog> builder)
    {
        builder.ToTable("notification_logs");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("log_id");
        builder.Property(n => n.PhoneNumber).HasMaxLength(20);
        builder.Property(n => n.NotificationType).HasMaxLength(80).IsRequired();
        builder.Property(n => n.Channel).HasMaxLength(20).IsRequired();
        builder.Property(n => n.Title).HasMaxLength(200);
        builder.Property(n => n.Body).HasMaxLength(4000).IsRequired();
        builder.Property(n => n.Status).HasMaxLength(20).IsRequired();
        builder.Property(n => n.ExternalReference).HasMaxLength(512);
        builder.Property(n => n.ErrorMessage).HasMaxLength(2000);
        builder.Property(n => n.SentAt).IsRequired();
        builder.Property(n => n.IsRead).HasDefaultValue(false);

        builder.HasIndex(n => new { n.UserId, n.SentAt });
        builder.HasIndex(n => new { n.UserId, n.IsRead });
    }
}

public class MedicationReminderConfiguration : IEntityTypeConfiguration<MedicationReminder>
{
    public void Configure(EntityTypeBuilder<MedicationReminder> builder)
    {
        builder.ToTable("medication_reminders");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("reminder_id");
        builder.Property(r => r.MedicationName).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Dosage).HasMaxLength(200).IsRequired();
        builder.Property(r => r.Frequency).HasMaxLength(20).IsRequired();
        builder.Property(r => r.ReminderTimes).HasColumnType("text").IsRequired();
        builder.Property(r => r.IsActive).HasDefaultValue(true);

        builder.HasIndex(r => new { r.PatientId, r.IsActive });
    }
}

// ─── Patient Medical History ──────────────────────────────────────────────────

public class PatientMedicalHistoryConfiguration : IEntityTypeConfiguration<PatientMedicalHistory>
{
    public void Configure(EntityTypeBuilder<PatientMedicalHistory> builder)
    {
        builder.ToTable("patient_medical_histories");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).HasColumnName("history_id");
        builder.Property(h => h.ChronicConditions).HasColumnType("text").HasDefaultValue("[]").IsRequired();
        builder.Property(h => h.Allergies).HasColumnType("text").HasDefaultValue("[]").IsRequired();
        builder.Property(h => h.CurrentMedications).HasColumnType("text").HasDefaultValue("[]").IsRequired();
        builder.Property(h => h.FamilyHistory).HasColumnType("text").HasDefaultValue("{}").IsRequired();
        builder.Property(h => h.BloodType).HasMaxLength(5);
        builder.Property(h => h.AlcoholConsumption).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(h => h.PatientId).IsUnique();

        builder.HasOne(h => h.Patient).WithOne(p => p.StructuredMedicalHistory)
            .HasForeignKey<PatientMedicalHistory>(h => h.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Emergency Contact ────────────────────────────────────────────────────────

public class EmergencyContactConfiguration : IEntityTypeConfiguration<EmergencyContact>
{
    public void Configure(EntityTypeBuilder<EmergencyContact> builder)
    {
        builder.ToTable("emergency_contacts");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("contact_id");
        builder.Property(c => c.FullName).HasMaxLength(100).IsRequired();
        builder.Property(c => c.Relationship).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(c => c.PhoneNumber).HasMaxLength(20).IsRequired();
        builder.Property(c => c.IsPrimary).HasDefaultValue(false);

        builder.HasIndex(c => new { c.PatientId, c.IsPrimary });

        builder.HasOne(c => c.Patient).WithMany(p => p.EmergencyContacts)
            .HasForeignKey(c => c.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Health Record Attachment ─────────────────────────────────────────────────

public class HealthRecordAttachmentConfiguration : IEntityTypeConfiguration<HealthRecordAttachment>
{
    public void Configure(EntityTypeBuilder<HealthRecordAttachment> builder)
    {
        builder.ToTable("health_record_attachments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("attachment_id");
        builder.Property(a => a.FileName).HasMaxLength(255).IsRequired();
        builder.Property(a => a.FileType).HasMaxLength(50).IsRequired();
        builder.Property(a => a.StoragePath).HasMaxLength(2048).IsRequired();
        builder.Property(a => a.UploadedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");
        builder.ToTable(t => t.HasCheckConstraint("ck_file_size", "file_size_bytes BETWEEN 1 AND 10485760"));
        builder.ToTable(t => t.HasCheckConstraint("ck_file_type",
            "file_type IN ('application/pdf', 'image/jpeg', 'image/png')"));

        builder.HasIndex(a => a.HealthRecordId);

        builder.HasOne(a => a.HealthRecord).WithMany(h => h.Attachments)
            .HasForeignKey(a => a.HealthRecordId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Review ───────────────────────────────────────────────────────────────────

public class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("reviews");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("review_id");
        builder.Property(r => r.Rating).IsRequired();
        builder.Property(r => r.Comment).HasMaxLength(1000);
        builder.ToTable(t => t.HasCheckConstraint("ck_rating", "rating BETWEEN 1 AND 5"));
        builder.ToTable(t => t.HasCheckConstraint("ck_review_target",
            "(doctor_id IS NOT NULL AND center_id IS NULL) OR (doctor_id IS NULL AND center_id IS NOT NULL)"));

        builder.HasIndex(r => new { r.AppointmentId, r.DoctorId })
            .IsUnique().HasFilter("doctor_id IS NOT NULL");
        builder.HasIndex(r => new { r.AppointmentId, r.CenterId })
            .IsUnique().HasFilter("center_id IS NOT NULL");
        builder.HasIndex(r => r.DoctorId).HasFilter("doctor_id IS NOT NULL");
        builder.HasIndex(r => r.CenterId).HasFilter("center_id IS NOT NULL");

        builder.HasOne(r => r.Patient).WithMany(p => p.Reviews)
            .HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(r => r.Appointment).WithMany(a => a.Reviews)
            .HasForeignKey(r => r.AppointmentId).OnDelete(DeleteBehavior.Restrict);
    }
}

// ─── Favorite ─────────────────────────────────────────────────────────────────

public class FavoriteConfiguration : IEntityTypeConfiguration<Favorite>
{
    public void Configure(EntityTypeBuilder<Favorite> builder)
    {
        builder.ToTable("favorites");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("favorite_id");
        builder.ToTable(t => t.HasCheckConstraint("ck_favorite_target",
            "(doctor_id IS NOT NULL AND center_id IS NULL) OR (doctor_id IS NULL AND center_id IS NOT NULL)"));

        builder.HasIndex(f => new { f.PatientId, f.DoctorId })
            .IsUnique().HasFilter("doctor_id IS NOT NULL");
        builder.HasIndex(f => new { f.PatientId, f.CenterId })
            .IsUnique().HasFilter("center_id IS NOT NULL");

        builder.HasOne(f => f.Patient).WithMany(p => p.Favorites)
            .HasForeignKey(f => f.PatientId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Prescription Template ────────────────────────────────────────────────────

public class PrescriptionTemplateConfiguration : IEntityTypeConfiguration<PrescriptionTemplate>
{
    public void Configure(EntityTypeBuilder<PrescriptionTemplate> builder)
    {
        builder.ToTable("prescription_templates");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("template_id");
        builder.Property(t => t.Name).HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasMaxLength(1000);
        builder.Property(t => t.Diagnosis).HasMaxLength(500);
        builder.Property(t => t.Medications).HasColumnType("jsonb").IsRequired();
        builder.Property(t => t.LabTests).HasColumnType("jsonb");
        builder.Property(t => t.FollowUpInstructions).HasMaxLength(2000);
        builder.Property(t => t.UseCount).HasDefaultValue(0);
        builder.Property(t => t.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");

        builder.HasIndex(t => new { t.DoctorId, t.Name });
        builder.HasIndex(t => new { t.DoctorId, t.UseCount }).IsDescending(false, true);

        builder.HasOne(t => t.Doctor).WithMany(d => d.PrescriptionTemplates)
            .HasForeignKey(t => t.DoctorId).OnDelete(DeleteBehavior.Cascade);
    }
}

// ─── Appointment Note ─────────────────────────────────────────────────────────

public class AppointmentNoteConfiguration : IEntityTypeConfiguration<AppointmentNote>
{
    public void Configure(EntityTypeBuilder<AppointmentNote> builder)
    {
        builder.ToTable("appointment_notes");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("note_id");
        builder.Property(n => n.Content).HasMaxLength(5000).IsRequired();
        builder.Property(n => n.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");

        builder.HasIndex(n => n.AppointmentId).IsUnique();

        builder.HasOne(n => n.Appointment).WithMany()
            .HasForeignKey(n => n.AppointmentId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(n => n.Doctor).WithMany(d => d.AppointmentNotes)
            .HasForeignKey(n => n.DoctorId).OnDelete(DeleteBehavior.Restrict);
    }
}

// ─── Notification Preference ──────────────────────────────────────────────────

public class NotificationPreferenceConfiguration : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("notification_preferences");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.AppointmentRemindersPush).HasDefaultValue(true);
        builder.Property(p => p.AppointmentRemindersSms).HasDefaultValue(true);
        builder.Property(p => p.QueueUpdates).HasDefaultValue(true);
        builder.Property(p => p.HealthPredictionReady).HasDefaultValue(true);
        builder.Property(p => p.MedicationReminders).HasDefaultValue(true);
        builder.Property(p => p.PromotionalEmails).HasDefaultValue(false);

        builder.HasIndex(p => p.UserId).IsUnique();
    }
}

// ─── Refresh Token ────────────────────────────────────────────────────────────

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Token).HasMaxLength(256).IsRequired();
        builder.Property(r => r.ExpiresAt).IsRequired();
        builder.Property(r => r.CreatedAt).HasDefaultValueSql("TIMEZONE('utc', NOW())");

        builder.HasIndex(r => r.Token).IsUnique();
        builder.HasIndex(r => r.UserId);

        builder.HasOne(r => r.User).WithMany()
            .HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}
