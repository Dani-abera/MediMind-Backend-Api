using MediMind.Domain.Entities;
using MediMind.Domain.Enums;

namespace MediMind.Domain.Common.Interfaces;

// ─── Unit of Work ─────────────────────────────────────────────────────────────

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}

// ─── Generic Repository ───────────────────────────────────────────────────────

public interface IRepository<T> where T : class
{
    Task<T?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(T entity, CancellationToken ct = default);
    Task UpdateAsync(T entity, CancellationToken ct = default);
    Task DeleteAsync(T entity, CancellationToken ct = default);
}

// ─── Specific Repositories ────────────────────────────────────────────────────

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByPhoneAsync(string phone, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct = default);
}

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Patient?> GetWithHealthRecordsAsync(Guid patientId, CancellationToken ct = default);
}

public interface IDoctorRepository : IRepository<Doctor>
{
    Task<Doctor?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetBySpecializationAsync(string specialization, CancellationToken ct = default);
    Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default);
}

public interface IHealthcareCenterRepository : IRepository<HealthcareCenter>
{
    Task<HealthcareCenter?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<IReadOnlyList<HealthcareCenter>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<HealthcareCenter?> GetWithAdminsAsync(Guid centerId, CancellationToken ct = default);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<bool> IsSlotAvailableAsync(Guid doctorId, Guid centerId, DateOnly date, TimeOnly time, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByDoctorAndDateAsync(Guid doctorId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByCenterAndDateAsync(Guid centerId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetPendingByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetConfirmedForQueueGenerationAsync(DateOnly date, CancellationToken ct = default);
    Task<bool> PatientHasAppointmentTodayAsync(Guid patientId, Guid doctorId, DateOnly date, CancellationToken ct = default);
}

public interface IQueueRepository : IRepository<QueueEntry>
{
    Task<IReadOnlyList<QueueEntry>> GetByCenterAndDateAsync(Guid centerId, DateOnly date, CancellationToken ct = default);
    Task<QueueEntry?> GetNextWaitingAsync(Guid centerId, CancellationToken ct = default);
    Task<QueueEntry?> GetByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
    Task BulkInsertAsync(IEnumerable<QueueEntry> entries, CancellationToken ct = default);
    Task UpdatePositionsAsync(Guid centerId, DateOnly date, CancellationToken ct = default);
}

public interface IHealthRecordRepository : IRepository<HealthRecord>
{
    Task<IReadOnlyList<HealthRecord>> GetByPatientAsync(Guid patientId, int days = 30, CancellationToken ct = default);
    Task<HealthRecord?> GetLatestByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<int> CountByPatientAsync(Guid patientId, CancellationToken ct = default);
}

public interface IHealthPredictionRepository : IRepository<HealthPrediction>
{
    Task<HealthPrediction?> GetLatestByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<HealthPrediction>> GetHistoryByPatientAsync(Guid patientId, CancellationToken ct = default);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByRefAsync(string paymentRef, CancellationToken ct = default);
    Task<bool> ExistsByRefAsync(string paymentRef, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
}
