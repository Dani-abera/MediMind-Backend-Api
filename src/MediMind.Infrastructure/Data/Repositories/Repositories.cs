using Microsoft.EntityFrameworkCore;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;

namespace MediMind.Infrastructure.Data.Repositories;

// ─── Generic Repository ───────────────────────────────────────────────────────

public class Repository<T>(MediMindDbContext context) : IRepository<T> where T : class
{
    protected readonly MediMindDbContext Db = context;
    protected readonly DbSet<T> DbSet = context.Set<T>();

    public virtual async Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default) =>
        await DbSet.FindAsync([id], ct);

    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default) =>
        await DbSet.ToListAsync(ct);

    public virtual async Task AddAsync(T entity, CancellationToken ct = default) =>
        await DbSet.AddAsync(entity, ct);

    public virtual Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        DbSet.Update(entity);
        return Task.CompletedTask;
    }

    public virtual Task DeleteAsync(T entity, CancellationToken ct = default)
    {
        DbSet.Remove(entity);
        return Task.CompletedTask;
    }
}

// ─── User Repository ──────────────────────────────────────────────────────────

public class UserRepository(MediMindDbContext context)
    : Repository<User>(context), IUserRepository
{
    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default) =>
        await Db.Users.FirstOrDefaultAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default) =>
        await Db.Users.FirstOrDefaultAsync(u => u.PhoneNumber == phone, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default) =>
        await Db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), ct);

    public async Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct = default) =>
        await Db.Users.AnyAsync(u => u.PhoneNumber == phone, ct);
}

// ─── Patient Repository ───────────────────────────────────────────────────────

public class PatientRepository(MediMindDbContext context)
    : Repository<Patient>(context), IPatientRepository
{
    public async Task<Patient?> GetByUserIdAsync(Guid userId, CancellationToken ct = default) =>
        await Db.Patients.FirstOrDefaultAsync(p => p.Id == userId, ct);

    public async Task<Patient?> GetWithHealthRecordsAsync(Guid patientId, CancellationToken ct = default) =>
        await Db.Patients
            .Include(p => p.HealthRecords)
            .Include(p => p.HealthPredictions)
            .FirstOrDefaultAsync(p => p.Id == patientId, ct);
}

// ─── Doctor Repository ────────────────────────────────────────────────────────

public class DoctorRepository(MediMindDbContext context)
    : Repository<Doctor>(context), IDoctorRepository
{
    public async Task<Doctor?> GetByBadgeNumberAsync(string badgeNumber, CancellationToken ct = default) =>
        await Db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.BadgeNumber == badgeNumber, ct);

    public async Task<Doctor?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default) =>
        await Db.Doctors
            .Include(d => d.Schedules)
            .FirstOrDefaultAsync(d => d.LicenseNumber == licenseNumber, ct);

    public async Task<IReadOnlyList<Doctor>> GetByCenterAsync(Guid centerId, CancellationToken ct = default) =>
        await Db.Doctors
            .Where(d => d.DoctorHealthcareCenters.Any(dhc => dhc.CenterId == centerId && dhc.IsActive))
            .Include(d => d.Schedules.Where(s => s.CenterId == centerId))
            .Include(d => d.DoctorHealthcareCenters.Where(dhc => dhc.CenterId == centerId))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Doctor>> GetBySpecializationAsync(
        string specialization, CancellationToken ct = default) =>
        await Db.Doctors
            .Where(d => d.Specialization.ToLower().Contains(specialization.ToLower()) &&
                        d.Status == UserStatus.Active)
            .ToListAsync(ct);

    public async Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default) =>
        await Db.Doctors.AnyAsync(d => d.LicenseNumber == licenseNumber, ct);
}

public class OtpVerificationRepository(MediMindDbContext context)
    : Repository<OtpVerification>(context), IOtpVerificationRepository
{
    public async Task<OtpVerification?> GetLatestActiveAsync(string phoneNumber, string purpose, CancellationToken ct = default) =>
        await Db.OtpVerifications
            .Where(x => x.PhoneNumber == phoneNumber && x.Purpose == purpose && !x.IsUsed)
            .OrderByDescending(x => x.CreatedAt)
            .FirstOrDefaultAsync(ct);
}

// ─── Healthcare Center Repository ─────────────────────────────────────────────

public class HealthcareCenterRepository(MediMindDbContext context)
    : Repository<HealthcareCenter>(context), IHealthcareCenterRepository
{
    public async Task<HealthcareCenter?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default) =>
        await Db.HealthcareCenters.FirstOrDefaultAsync(c => c.LicenseNumber == licenseNumber, ct);

    public async Task<IReadOnlyList<HealthcareCenter>> GetActiveSubscriptionsAsync(CancellationToken ct = default) =>
        await Db.HealthcareCenters
            .Where(c => c.SubscriptionStatus == SubscriptionStatus.Active)
            .ToListAsync(ct);

    public async Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default) =>
        await Db.HealthcareCenters.AnyAsync(c => c.LicenseNumber == licenseNumber, ct);

    public async Task<HealthcareCenter?> GetWithAdminsAsync(Guid centerId, CancellationToken ct = default) =>
        await Db.HealthcareCenters
            .Include(c => c.Admins)
            .Include(c => c.DoctorHealthcareCenters)
                .ThenInclude(dhc => dhc.Doctor)
            .FirstOrDefaultAsync(c => c.Id == centerId, ct);
}

// ─── Appointment Repository ───────────────────────────────────────────────────

public class AppointmentRepository(MediMindDbContext context)
    : Repository<Appointment>(context), IAppointmentRepository
{
    public async Task<bool> IsSlotAvailableAsync(
        Guid doctorId, Guid centerId, DateOnly date, TimeOnly time, CancellationToken ct = default) =>
        !await Db.Appointments.AnyAsync(a =>
            a.DoctorId == doctorId &&
            a.CenterId == centerId &&
            a.AppointmentDate == date &&
            a.AppointmentTime == time &&
            a.Status != AppointmentStatus.Cancelled &&
            a.Status != AppointmentStatus.NoShow, ct);

    public async Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await Db.Appointments
            .Where(a => a.PatientId == patientId)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .OrderByDescending(a => a.AppointmentDate).ThenByDescending(a => a.AppointmentTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetByDoctorAndDateAsync(
        Guid doctorId, DateOnly date, CancellationToken ct = default) =>
        await Db.Appointments
            .Where(a => a.DoctorId == doctorId && a.AppointmentDate == date)
            .OrderBy(a => a.AppointmentTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetByCenterAndDateAsync(
        Guid centerId, DateOnly date, CancellationToken ct = default) =>
        await Db.Appointments
            .Where(a => a.CenterId == centerId && a.AppointmentDate == date)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.QueueEntry)
            .OrderBy(a => a.AppointmentTime)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetPendingByCenterAsync(
        Guid centerId, CancellationToken ct = default) =>
        await Db.Appointments
            .Where(a => a.CenterId == centerId && a.Status == AppointmentStatus.Pending)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .OrderBy(a => a.BookingDate)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<Appointment>> GetConfirmedForQueueGenerationAsync(
        DateOnly date, CancellationToken ct = default) =>
        await Db.Appointments
            .Where(a => a.AppointmentDate == date && a.Status == AppointmentStatus.Confirmed)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .OrderBy(a => a.CenterId).ThenBy(a => a.AppointmentTime)
            .ToListAsync(ct);

    public async Task<bool> PatientHasAppointmentTodayAsync(
        Guid patientId, Guid doctorId, DateOnly date, CancellationToken ct = default) =>
        await Db.Appointments.AnyAsync(a =>
            a.PatientId == patientId &&
            a.DoctorId == doctorId &&
            a.AppointmentDate == date &&
            a.Status != AppointmentStatus.Cancelled, ct);
}

// ─── Queue Repository ─────────────────────────────────────────────────────────

public class QueueRepository(MediMindDbContext context)
    : Repository<QueueEntry>(context), IQueueRepository
{
    public async Task<IReadOnlyList<QueueEntry>> GetByCenterAndDateAsync(
        Guid centerId, DateOnly date, CancellationToken ct = default) =>
        await Db.QueueEntries
            .Where(q => q.CenterId == centerId && q.QueueDate == date)
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

    public async Task<QueueEntry?> GetNextWaitingAsync(Guid centerId, CancellationToken ct = default) =>
        await Db.QueueEntries
            .Where(q => q.CenterId == centerId &&
                        q.QueueDate == DateOnly.FromDateTime(DateTime.UtcNow) &&
                        q.Status == QueueStatus.Waiting)
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync(ct);

    public async Task<QueueEntry?> GetByAppointmentAsync(Guid appointmentId, CancellationToken ct = default) =>
        await Db.QueueEntries.FirstOrDefaultAsync(q => q.AppointmentId == appointmentId, ct);

    public async Task BulkInsertAsync(IEnumerable<QueueEntry> entries, CancellationToken ct = default) =>
        await Db.QueueEntries.AddRangeAsync(entries, ct);

    public async Task UpdatePositionsAsync(Guid centerId, DateOnly date, CancellationToken ct = default)
    {
        // Recalculate positions for all waiting patients in this center's queue
        var waitingEntries = await Db.QueueEntries
            .Where(q => q.CenterId == centerId && q.QueueDate == date && q.Status == QueueStatus.Waiting)
            .OrderBy(q => q.Position)
            .ToListAsync(ct);

        for (int i = 0; i < waitingEntries.Count; i++)
            waitingEntries[i].UpdatePosition(i + 1, 30); // Default 30 min slot

        Db.QueueEntries.UpdateRange(waitingEntries);
    }
}

// ─── Health Record Repository ─────────────────────────────────────────────────

public class HealthRecordRepository(MediMindDbContext context)
    : Repository<HealthRecord>(context), IHealthRecordRepository
{
    public async Task<IReadOnlyList<HealthRecord>> GetByPatientAsync(
        Guid patientId, int days = 30, CancellationToken ct = default)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        return await Db.HealthRecords
            .Where(h => h.PatientId == patientId && h.RecordDate >= since)
            .OrderByDescending(h => h.RecordDate).ThenByDescending(h => h.RecordTime)
            .ToListAsync(ct);
    }

    public async Task<HealthRecord?> GetLatestByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await Db.HealthRecords
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.RecordDate).ThenByDescending(h => h.RecordTime)
            .FirstOrDefaultAsync(ct);

    public async Task<int> CountByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await Db.HealthRecords.CountAsync(h => h.PatientId == patientId, ct);
}

// ─── Health Prediction Repository ────────────────────────────────────────────

public class HealthPredictionRepository(MediMindDbContext context)
    : Repository<HealthPrediction>(context), IHealthPredictionRepository
{
    public async Task<HealthPrediction?> GetLatestByPatientAsync(Guid patientId, CancellationToken ct = default) =>
        await Db.HealthPredictions
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.PredictionDate).ThenByDescending(h => h.PredictionTime)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<HealthPrediction>> GetHistoryByPatientAsync(
        Guid patientId, CancellationToken ct = default) =>
        await Db.HealthPredictions
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.PredictionDate)
            .ToListAsync(ct);
}

// ─── Payment Repository ───────────────────────────────────────────────────────

public class PaymentRepository(MediMindDbContext context)
    : Repository<Payment>(context), IPaymentRepository
{
    public async Task<Payment?> GetByRefAsync(string paymentRef, CancellationToken ct = default) =>
        await Db.Payments.FirstOrDefaultAsync(p => p.PaymentRef == paymentRef, ct);

    public async Task<bool> ExistsByRefAsync(string paymentRef, CancellationToken ct = default) =>
        await Db.Payments.AnyAsync(p => p.PaymentRef == paymentRef, ct);

    public async Task<IReadOnlyList<Payment>> GetByAppointmentAsync(
        Guid appointmentId, CancellationToken ct = default) =>
        await Db.Payments.Where(p => p.AppointmentId == appointmentId).ToListAsync(ct);
}
