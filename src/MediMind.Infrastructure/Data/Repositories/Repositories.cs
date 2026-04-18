using Microsoft.EntityFrameworkCore;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using Npgsql;

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
    public async Task<Appointment?> GetByIdAsync(Guid appointmentId) =>
        await Db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .FirstOrDefaultAsync(a => a.Id == appointmentId);

    public async Task<Appointment?> GetByIdForPatientAsync(Guid appointmentId, Guid patientId) =>
        await Db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .FirstOrDefaultAsync(a => a.Id == appointmentId && a.PatientId == patientId);

    public async Task<PagedResult<Appointment>> GetByPatientAsync(Guid patientId, AppointmentFilterDto filter)
    {
        var query = Db.Appointments
            .Where(a => a.PatientId == patientId)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .AsQueryable();
        query = ApplyFilter(query, filter);
        return await BuildPagedResult(query, filter);
    }

    public async Task<PagedResult<Appointment>> GetByCenterAsync(Guid centerId, AppointmentFilterDto filter)
    {
        var query = Db.Appointments
            .Where(a => a.CenterId == centerId)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .AsQueryable();
        query = ApplyFilter(query, filter);
        return await BuildPagedResult(query, filter);
    }

    public async Task<PagedResult<Appointment>> GetByDoctorAsync(Guid doctorId, Guid centerId, AppointmentFilterDto filter)
    {
        var query = Db.Appointments
            .Where(a => a.DoctorId == doctorId && a.CenterId == centerId)
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Include(a => a.QueueEntry)
            .AsQueryable();
        query = ApplyFilter(query, filter);
        return await BuildPagedResult(query, filter);
    }

    public async Task<Appointment> CreateAsync(Appointment appointment)
    {
        await Db.Appointments.AddAsync(appointment);
        await Db.SaveChangesAsync();
        return appointment;
    }

    public async Task<Appointment?> UpdateStatusAsync(Guid appointmentId, AppointmentStatus status, Guid updatedBy)
    {
        var appointment = await Db.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment is null)
            return null;

        appointment.UpdateStatus(status, updatedBy);
        await Db.SaveChangesAsync();
        return appointment;
    }

    public async Task<bool> HasConflictAsync(Guid doctorId, Guid centerId, DateOnly date, TimeOnly time, Guid? excludeAppointmentId = null) =>
        await Db.Appointments.AnyAsync(a =>
            a.DoctorId == doctorId &&
            a.CenterId == centerId &&
            a.AppointmentDate == date &&
            a.AppointmentTime == time &&
            a.Status != AppointmentStatus.Cancelled &&
            (!excludeAppointmentId.HasValue || a.Id != excludeAppointmentId.Value));

    public async Task<int> GetRescheduleCountAsync(Guid appointmentId) =>
        await Db.Appointments
            .Where(a => a.Id == appointmentId)
            .Select(a => a.RescheduleCount)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<Appointment>> GetUpcomingForReminderAsync(DateTime reminderTime, ReminderType type)
    {
        var lower = reminderTime.AddMinutes(-5);
        var upper = reminderTime.AddMinutes(5);

        return await Db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Doctor)
            .Include(a => a.Center)
            .Where(a => a.Status == AppointmentStatus.Confirmed)
            .Where(a => a.AppointmentDate.ToDateTime(a.AppointmentTime) >= lower &&
                        a.AppointmentDate.ToDateTime(a.AppointmentTime) <= upper)
            .Where(a => type == ReminderType.TwentyFourHours
                ? a.Reminder24hSentAt == null
                : a.Reminder2hSentAt == null)
            .ToListAsync();
    }

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

    private static IQueryable<Appointment> ApplyFilter(IQueryable<Appointment> query, AppointmentFilterDto filter)
    {
        if (filter.Status.HasValue)
            query = query.Where(a => a.Status == filter.Status.Value);
        if (filter.StartDate.HasValue)
            query = query.Where(a => a.AppointmentDate >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            query = query.Where(a => a.AppointmentDate <= filter.EndDate.Value);
        if (filter.DoctorId.HasValue)
            query = query.Where(a => a.DoctorId == filter.DoctorId.Value);

        return query;
    }

    private static async Task<PagedResult<Appointment>> BuildPagedResult(IQueryable<Appointment> query, AppointmentFilterDto filter)
    {
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(a => a.AppointmentDate)
            .ThenByDescending(a => a.AppointmentTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        return new PagedResult<Appointment>(items, page, pageSize, total);
    }
}

public class DoctorScheduleRepository(MediMindDbContext context)
    : Repository<DoctorSchedule>(context), IDoctorScheduleRepository
{
    public async Task<DoctorSchedule?> GetByDoctorAndCenterAsync(Guid doctorId, Guid centerId) =>
        await Db.DoctorSchedules.FirstOrDefaultAsync(s => s.DoctorId == doctorId && s.CenterId == centerId);

    public async Task<DoctorSchedule> CreateAsync(DoctorSchedule schedule)
    {
        await Db.DoctorSchedules.AddAsync(schedule);
        await Db.SaveChangesAsync();
        return schedule;
    }

    public async Task<DoctorSchedule?> UpdateAsync(DoctorSchedule schedule)
    {
        var existing = await Db.DoctorSchedules.FirstOrDefaultAsync(s => s.Id == schedule.Id);
        if (existing is null)
            return null;

        Db.DoctorSchedules.Update(schedule);
        await Db.SaveChangesAsync();
        return schedule;
    }

    public async Task<bool> DeleteAsync(Guid scheduleId)
    {
        var schedule = await Db.DoctorSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule is null)
            return false;

        Db.DoctorSchedules.Remove(schedule);
        await Db.SaveChangesAsync();
        return true;
    }
}

// ─── Queue Repository ─────────────────────────────────────────────────────────

public class QueueRepository(MediMindDbContext context)
    : Repository<QueueEntry>(context), IQueueRepository
{
    public async Task<QueueEntry?> GetByAppointmentIdAsync(Guid appointmentId) =>
        await Db.QueueEntries
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .Include(q => q.Appointment).ThenInclude(a => a.Doctor)
            .Include(q => q.Appointment).ThenInclude(a => a.Center)
            .FirstOrDefaultAsync(q => q.AppointmentId == appointmentId);

    public async Task<QueueEntry?> GetByIdAsync(Guid queueId) =>
        await Db.QueueEntries
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .Include(q => q.Appointment).ThenInclude(a => a.Doctor)
            .Include(q => q.Appointment).ThenInclude(a => a.Center)
            .FirstOrDefaultAsync(q => q.Id == queueId);

    public async Task<IEnumerable<QueueEntry>> GetCenterQueueAsync(Guid centerId, DateOnly date) =>
        await Db.QueueEntries
            .Where(q => q.CenterId == centerId && q.QueueDate == date)
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .Include(q => q.Appointment).ThenInclude(a => a.Doctor)
            .Include(q => q.Appointment).ThenInclude(a => a.Center)
            .OrderBy(q => q.Position)
            .ToListAsync();

    public async Task<QueueEntry?> GetNextWaitingAsync(Guid centerId, DateOnly date) =>
        await Db.QueueEntries
            .Where(q => q.CenterId == centerId &&
                        q.QueueDate == date &&
                        q.Status == QueueStatus.Waiting)
            .Include(q => q.Appointment).ThenInclude(a => a.Patient)
            .Include(q => q.Appointment).ThenInclude(a => a.Doctor)
            .Include(q => q.Appointment).ThenInclude(a => a.Center)
            .OrderBy(q => q.Position)
            .FirstOrDefaultAsync();

    public async Task<QueueEntry> CreateAsync(QueueEntry entry)
    {
        await Db.QueueEntries.AddAsync(entry);
        return entry;
    }

    public async Task<QueueEntry?> UpdateStatusAsync(Guid queueId, QueueStatus status)
    {
        var queue = await Db.QueueEntries.FirstOrDefaultAsync(q => q.Id == queueId);
        if (queue is null)
            return null;

        switch (status)
        {
            case QueueStatus.Called:
                queue.CallPatient();
                break;
            case QueueStatus.InConsultation:
                queue.StartConsultation();
                break;
            case QueueStatus.Completed:
                queue.CompleteConsultation();
                break;
            case QueueStatus.Missed:
                queue.MarkMissed();
                break;
            default:
                break;
        }

        return queue;
    }

    public async Task RecalculatePositionsAsync(Guid centerId, DateOnly date)
    {
        var centerParam = new Npgsql.NpgsqlParameter("centerId", centerId);
        var dateParam = new Npgsql.NpgsqlParameter("queueDate", date);
        await Db.Database.ExecuteSqlRawAsync(
            \"\"\"\n            UPDATE queue SET position = rn.new_position\n            FROM (\n              SELECT queue_id, ROW_NUMBER() OVER (ORDER BY position) as new_position\n              FROM queue\n              WHERE center_id = @centerId AND queue_date = @queueDate\n              AND status IN ('Waiting', 'Called')\n            ) rn\n            WHERE queue.queue_id = rn.queue_id;\n            \"\"\", centerParam, dateParam);

        var center = await Db.HealthcareCenters.FirstOrDefaultAsync(c => c.Id == centerId);
        var slot = center?.SlotDurationMinutes ?? 30;
        var active = await Db.QueueEntries
            .Where(q => q.CenterId == centerId && q.QueueDate == date && (q.Status == QueueStatus.Waiting || q.Status == QueueStatus.Called))
            .OrderBy(q => q.Position)
            .ToListAsync();
        for (var i = 0; i < active.Count; i++)
            active[i].UpdatePosition(i + 1, slot);
    }

    public async Task<int> GetCurrentPositionAsync(Guid appointmentId) =>
        await Db.QueueEntries
            .Where(q => q.AppointmentId == appointmentId)
            .Select(q => q.Position)
            .FirstOrDefaultAsync();

    public async Task<int> GetEstimatedWaitAsync(Guid appointmentId) =>
        await Db.QueueEntries
            .Where(q => q.AppointmentId == appointmentId)
            .Select(q => q.EstimatedWaitTimeMinutes)
            .FirstOrDefaultAsync();

    public async Task BulkCreateAsync(IEnumerable<QueueEntry> entries) =>
        await Db.QueueEntries.AddRangeAsync(entries);

    public async Task<bool> ExistsForDateAsync(Guid centerId, DateOnly date) =>
        await Db.QueueEntries.AnyAsync(q => q.CenterId == centerId && q.QueueDate == date);

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
        await RecalculatePositionsAsync(centerId, date);
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

    public async Task<HealthRecord?> GetByIdAsync(Guid recordId, Guid patientId) =>
        await Db.HealthRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == recordId && h.PatientId == patientId);

    public async Task<IEnumerable<HealthRecord>> GetByPatientIdAsync(
        Guid patientId,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize)
    {
        var query = Db.HealthRecords
            .AsNoTracking()
            .Where(h => h.PatientId == patientId);

        if (startDate.HasValue)
            query = query.Where(h => h.RecordDate >= startDate.Value);
        if (endDate.HasValue)
            query = query.Where(h => h.RecordDate <= endDate.Value);

        return await query
            .OrderByDescending(h => h.RecordDate)
            .ThenByDescending(h => h.RecordTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
    }

    public async Task<HealthRecord> CreateAsync(HealthRecord record)
    {
        await Db.HealthRecords.AddAsync(record);
        await Db.SaveChangesAsync();
        return record;
    }

    public async Task<HealthRecord?> UpdateAsync(HealthRecord record)
    {
        var exists = await Db.HealthRecords.AnyAsync(x => x.Id == record.Id && x.PatientId == record.PatientId);
        if (!exists)
            return null;

        Db.HealthRecords.Update(record);
        await Db.SaveChangesAsync();
        return record;
    }

    public async Task<bool> DeleteAsync(Guid recordId, Guid patientId)
    {
        var record = await Db.HealthRecords.FirstOrDefaultAsync(h => h.Id == recordId && h.PatientId == patientId);
        if (record is null)
            return false;

        Db.HealthRecords.Remove(record);
        await Db.SaveChangesAsync();
        return true;
    }

    public async Task<HealthTrendDto> GetTrendAsync(Guid patientId, int days)
    {
        var since = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        var records = await Db.HealthRecords
            .AsNoTracking()
            .Where(h => h.PatientId == patientId && h.RecordDate >= since)
            .OrderBy(h => h.RecordDate)
            .ThenBy(h => h.RecordTime)
            .ToListAsync();

        var half = records.Count / 2;
        var firstHalfSystolic = records.Take(half).Where(x => x.SystolicBp.HasValue).Select(x => x.SystolicBp!.Value).ToList();
        var secondHalfSystolic = records.Skip(half).Where(x => x.SystolicBp.HasValue).Select(x => x.SystolicBp!.Value).ToList();

        var firstAvg = firstHalfSystolic.Count > 0 ? firstHalfSystolic.Average() : (double?)null;
        var secondAvg = secondHalfSystolic.Count > 0 ? secondHalfSystolic.Average() : (double?)null;

        var trend = "Stable";
        if (firstAvg.HasValue && secondAvg.HasValue)
        {
            if (secondAvg.Value < firstAvg.Value - 5)
                trend = "Improving";
            else if (secondAvg.Value > firstAvg.Value + 5)
                trend = "Worsening";
        }

        return new HealthTrendDto(
            $"Last {days} Days",
            records.Where(x => x.SystolicBp.HasValue).Select(x => (double?)x.SystolicBp!.Value).Average(),
            records.Where(x => x.DiastolicBp.HasValue).Select(x => (double?)x.DiastolicBp!.Value).Average(),
            records.Where(x => x.GlucoseLevel.HasValue).Select(x => (double?)x.GlucoseLevel!.Value).Average(),
            records.Where(x => x.Weight.HasValue).Select(x => (double?)x.Weight!.Value).Average(),
            records.Where(x => x.SystolicBp.HasValue).Select(x => x.SystolicBp).Min(),
            records.Where(x => x.SystolicBp.HasValue).Select(x => x.SystolicBp).Max(),
            records.Count,
            trend);
    }

    public async Task<int> GetRecordCountAsync(Guid patientId) =>
        await Db.HealthRecords.AsNoTracking().CountAsync(x => x.PatientId == patientId);

    public async Task<HealthRecord?> GetLatestAsync(Guid patientId) =>
        await Db.HealthRecords
            .AsNoTracking()
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.RecordDate)
            .ThenByDescending(h => h.RecordTime)
            .FirstOrDefaultAsync();

    public async Task<IEnumerable<HealthRecord>> GetAllForPredictionAsync(Guid patientId) =>
        await Db.HealthRecords
            .AsNoTracking()
            .Where(h => h.PatientId == patientId)
            .OrderBy(h => h.RecordDate)
            .ThenBy(h => h.RecordTime)
            .ToListAsync();
}

// ─── Health Prediction Repository ────────────────────────────────────────────

public class HealthPredictionRepository(MediMindDbContext context)
    : Repository<HealthPrediction>(context), IHealthPredictionRepository
{
    public async Task<HealthPrediction?> GetByIdAsync(Guid predictionId, Guid patientId) =>
        await Db.HealthPredictions
            .AsNoTracking()
            .FirstOrDefaultAsync(h => h.Id == predictionId && h.PatientId == patientId);

    public async Task<IEnumerable<HealthPrediction>> GetByPatientIdAsync(Guid patientId, int page, int pageSize) =>
        await Db.HealthPredictions
            .AsNoTracking()
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.PredictionDate).ThenByDescending(h => h.PredictionTime)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

    public async Task<HealthPrediction?> GetLatestAsync(Guid patientId) =>
        await Db.HealthPredictions
            .AsNoTracking()
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.PredictionDate)
            .ThenByDescending(h => h.PredictionTime)
            .FirstOrDefaultAsync();

    public async Task<HealthPrediction> CreateAsync(HealthPrediction prediction, IEnumerable<Guid> healthRecordIds)
    {
        await Db.Database.BeginTransactionAsync();
        try
        {
            await Db.HealthPredictions.AddAsync(prediction);
            await Db.SaveChangesAsync();

            var links = healthRecordIds
                .Distinct()
                .Select(recordId => new HealthPredictionRecord(prediction.Id, recordId))
                .ToList();

            await Db.HealthPredictionRecords.AddRangeAsync(links);
            await Db.SaveChangesAsync();
            await Db.Database.CommitTransactionAsync();

            return prediction;
        }
        catch
        {
            await Db.Database.RollbackTransactionAsync();
            throw;
        }
    }

    public async Task<IEnumerable<HealthPrediction>> GetHistoryAsync(Guid patientId, int count) =>
        await Db.HealthPredictions
            .AsNoTracking()
            .Where(h => h.PatientId == patientId)
            .OrderByDescending(h => h.PredictionDate)
            .ThenByDescending(h => h.PredictionTime)
            .Take(count)
            .ToListAsync();
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
