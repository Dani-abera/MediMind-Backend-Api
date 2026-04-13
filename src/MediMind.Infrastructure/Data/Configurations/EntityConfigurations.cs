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
        builder.HasIndex(u => u.PhoneNumber).IsUnique();
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
            .HasForeignKey(p => p.PatientId).OnDelete(DeleteBehavior.Restrict);
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

        builder.Property(c => c.SubscriptionStatus).HasConversion<string>().HasMaxLength(20);
        builder.Property(c => c.Latitude).HasPrecision(10, 8);
        builder.Property(c => c.Longitude).HasPrecision(11, 8);
        builder.Property(c => c.ProfileImageUrl).HasMaxLength(1024);

        // Business rule: slot duration must be 15, 30, 45, or 60
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_slot_duration", "slot_duration_minutes IN (15, 30, 45, 60)"));
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_advance_booking", "advance_booking_days BETWEEN 1 AND 90"));

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

// ─── Appointment ─────────────────────────────────────────────────────────────

public class AppointmentConfiguration : IEntityTypeConfiguration<Appointment>
{
    public void Configure(EntityTypeBuilder<Appointment> builder)
    {
        builder.ToTable("appointments");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).HasColumnName("appointment_id");
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(a => a.ReasonForVisit).IsRequired();
        builder.Property(a => a.DurationMinutes).HasDefaultValue(30);
        builder.ToTable(t => t.HasCheckConstraint(
            "ck_duration_minutes", "duration_minutes IN (15, 30, 45, 60)"));

        // THE most critical unique constraint: prevent double-booking
        builder.HasIndex(a => new { a.DoctorId, a.CenterId, a.AppointmentDate, a.AppointmentTime })
            .IsUnique()
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
            .HasForeignKey(p => p.AppointmentId).OnDelete(DeleteBehavior.Restrict);
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
        builder.Property(h => h.GlucoseLevel).HasPrecision(5, 2);
        builder.Property(h => h.Weight).HasPrecision(5, 2);
        builder.Property(h => h.Height).HasPrecision(5, 2);
        builder.Property(h => h.Temperature).HasPrecision(4, 2);
        builder.Property(h => h.RecordedBy).HasMaxLength(50).HasDefaultValue("patient");

        // Domain invariants as DB constraints
        builder.ToTable(t => t.HasCheckConstraint("ck_systolic_bp", "systolic_bp BETWEEN 70 AND 250 OR systolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_diastolic_bp", "diastolic_bp BETWEEN 40 AND 150 OR diastolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_bp_relation", "systolic_bp > diastolic_bp OR systolic_bp IS NULL OR diastolic_bp IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_glucose", "glucose_level BETWEEN 30 AND 600 OR glucose_level IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_temperature", "temperature BETWEEN 35 AND 43 OR temperature IS NULL"));
        builder.ToTable(t => t.HasCheckConstraint("ck_heart_rate", "heart_rate BETWEEN 30 AND 250 OR heart_rate IS NULL"));

        builder.Ignore(h => h.Bmi); // Computed property — not stored in DB

        builder.HasIndex(h => new { h.PatientId, h.RecordDate });
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
        builder.ToTable(t => t.HasCheckConstraint("ck_data_points", "data_points_used > 0"));

        builder.HasIndex(h => new { h.PatientId, h.PredictionDate });

        builder.HasMany(h => h.PredictionRecords).WithOne(r => r.Prediction)
            .HasForeignKey(r => r.PredictionId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class HealthPredictionRecordConfiguration : IEntityTypeConfiguration<HealthPredictionRecord>
{
    public void Configure(EntityTypeBuilder<HealthPredictionRecord> builder)
    {
        builder.ToTable("health_prediction_records");
        builder.HasKey(r => new { r.PredictionId, r.RecordId });
        builder.HasIndex(r => new { r.PredictionId, r.RecordId }).IsUnique();
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
        builder.Property(p => p.PaymentMethod).HasConversion<string>().HasMaxLength(50);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(p => p.ChapaTransactionId).HasMaxLength(100);
        builder.ToTable(t => t.HasCheckConstraint("ck_payment_amount", "amount > 0"));

        builder.HasIndex(p => p.PaymentRef).IsUnique();
        builder.HasIndex(p => p.AppointmentId);
        builder.HasIndex(p => p.PatientId);
        builder.HasIndex(p => p.Status);
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
