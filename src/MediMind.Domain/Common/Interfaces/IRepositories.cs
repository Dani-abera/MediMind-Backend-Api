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
    Task<Doctor?> GetByIdAsync(Guid doctorId);
    Task<Doctor?> GetByBadgeNumberAsync(string badgeNumber, CancellationToken ct = default);
    Task<Doctor?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<PagedResult<Doctor>> SearchAsync(DoctorSearchDto search);
    Task<IReadOnlyList<Doctor>> GetByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<IEnumerable<Doctor>> GetByCenterAsync(Guid centerId);
    Task<Doctor?> GetWithScheduleAsync(Guid doctorId, Guid centerId);
    Task<IEnumerable<Guid>> GetCenterIdsAsync(Guid doctorId);
    Task<IReadOnlyList<Doctor>> GetBySpecializationAsync(string specialization, CancellationToken ct = default);
    Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default);
}

public interface IOtpVerificationRepository : IRepository<OtpVerification>
{
    Task<OtpVerification?> GetLatestActiveAsync(string phoneNumber, string purpose, CancellationToken ct = default);
}

public interface IHealthcareCenterRepository : IRepository<HealthcareCenter>
{
    Task<HealthcareCenter?> GetByIdAsync(Guid centerId);
    Task<HealthcareCenter?> GetByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<HealthcareCenter?> GetByLicenseAsync(string licenseNumber);
    Task<PagedResult<HealthcareCenter>> SearchAsync(CenterSearchDto search);
    Task<HealthcareCenter> CreateAsync(HealthcareCenter center);
    Task<HealthcareCenter?> UpdateAsync(HealthcareCenter center);
    Task<bool> UpdateConfigurationAsync(Guid centerId, CenterConfigurationDto config);
    Task<IEnumerable<DoctorHealthcareCenter>> GetDoctorsAsync(Guid centerId);
    Task<bool> AddDoctorAsync(DoctorHealthcareCenter relation);
    Task<bool> RemoveDoctorAsync(Guid doctorId, Guid centerId);
    Task<IReadOnlyList<HealthcareCenter>> GetActiveSubscriptionsAsync(CancellationToken ct = default);
    Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<HealthcareCenter?> GetWithAdminsAsync(Guid centerId, CancellationToken ct = default);
}

public interface IAppointmentRepository : IRepository<Appointment>
{
    Task<Appointment?> GetByIdAsync(Guid appointmentId);
    Task<Appointment?> GetByIdForPatientAsync(Guid appointmentId, Guid patientId);
    Task<PagedResult<Appointment>> GetByPatientAsync(Guid patientId, AppointmentFilterDto filter);
    Task<PagedResult<Appointment>> GetByCenterAsync(Guid centerId, AppointmentFilterDto filter);
    Task<PagedResult<Appointment>> GetByDoctorAsync(Guid doctorId, Guid centerId, AppointmentFilterDto filter);
    Task<Appointment> CreateAsync(Appointment appointment);
    Task<Appointment?> UpdateStatusAsync(Guid appointmentId, AppointmentStatus status, Guid updatedBy);
    Task<bool> HasConflictAsync(Guid doctorId, Guid centerId, DateOnly date, TimeOnly time, Guid? excludeAppointmentId = null);
    Task<int> GetRescheduleCountAsync(Guid appointmentId);
    Task<IEnumerable<Appointment>> GetUpcomingForReminderAsync(DateTime reminderTime, ReminderType type);

    Task<bool> IsSlotAvailableAsync(Guid doctorId, Guid centerId, DateOnly date, TimeOnly time, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByDoctorAndDateAsync(Guid doctorId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetByCenterAndDateAsync(Guid centerId, DateOnly date, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetPendingByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<IReadOnlyList<Appointment>> GetConfirmedForQueueGenerationAsync(DateOnly date, CancellationToken ct = default);
    Task<bool> PatientHasAppointmentTodayAsync(Guid patientId, Guid doctorId, DateOnly date, CancellationToken ct = default);
}

public interface IDoctorScheduleRepository : IRepository<DoctorSchedule>
{
    Task<DoctorSchedule?> GetByDoctorAndCenterAsync(Guid doctorId, Guid centerId);
    Task<DoctorSchedule> CreateAsync(DoctorSchedule schedule);
    Task<DoctorSchedule?> UpdateAsync(DoctorSchedule schedule);
    Task<bool> DeleteAsync(Guid scheduleId);
}

public interface IQueueRepository : IRepository<QueueEntry>
{
    Task<QueueEntry?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<QueueEntry?> GetByIdAsync(Guid queueId);
    Task<IEnumerable<QueueEntry>> GetCenterQueueAsync(Guid centerId, DateOnly date);
    Task<QueueEntry?> GetNextWaitingAsync(Guid centerId, DateOnly date);
    Task<QueueEntry> CreateAsync(QueueEntry entry);
    Task<QueueEntry?> UpdateStatusAsync(Guid queueId, QueueStatus status);
    Task RecalculatePositionsAsync(Guid centerId, DateOnly date);
    Task<int> GetCurrentPositionAsync(Guid appointmentId);
    Task<int> GetEstimatedWaitAsync(Guid appointmentId);
    Task BulkCreateAsync(IEnumerable<QueueEntry> entries);
    Task<bool> ExistsForDateAsync(Guid centerId, DateOnly date);

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
    Task<HealthRecord?> GetByIdAsync(Guid recordId, Guid patientId);
    Task<IEnumerable<HealthRecord>> GetByPatientIdAsync(
        Guid patientId,
        DateOnly? startDate,
        DateOnly? endDate,
        int page,
        int pageSize);
    Task<HealthRecord> CreateAsync(HealthRecord record);
    Task<HealthRecord?> UpdateAsync(HealthRecord record);
    Task<bool> DeleteAsync(Guid recordId, Guid patientId);
    Task<HealthTrendDto> GetTrendAsync(Guid patientId, int days);
    Task<int> GetRecordCountAsync(Guid patientId);
    Task<HealthRecord?> GetLatestAsync(Guid patientId);
    Task<IEnumerable<HealthRecord>> GetAllForPredictionAsync(Guid patientId);
}

public interface IHealthPredictionRepository : IRepository<HealthPrediction>
{
    Task<HealthPrediction?> GetByIdAsync(Guid predictionId, Guid patientId);
    Task<IEnumerable<HealthPrediction>> GetByPatientIdAsync(Guid patientId, int page, int pageSize);
    Task<HealthPrediction?> GetLatestAsync(Guid patientId);
    Task<HealthPrediction> CreateAsync(HealthPrediction prediction, IEnumerable<Guid> healthRecordIds);
    Task<IEnumerable<HealthPrediction>> GetHistoryAsync(Guid patientId, int count);
}

public interface IPaymentRepository : IRepository<Payment>
{
    Task<Payment?> GetByIdAsync(Guid paymentId);
    Task<Payment?> GetByRefAsync(string paymentRef);
    Task<Payment?> GetByRefAsync(string paymentRef, CancellationToken ct);
    Task<Payment?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<Payment> CreateAsync(Payment payment);
    Task<Payment?> UpdateStatusAsync(Guid paymentId, PaymentStatus status, string? chapaTransactionId);
    Task<IEnumerable<Payment>> GetByPatientAsync(Guid patientId, int page, int pageSize);
    Task<IEnumerable<Payment>> GetByCenterAsync(Guid centerId, int page, int pageSize);
    Task<decimal> GetTotalRevenueAsync(Guid centerId, DateOnly startDate, DateOnly endDate);
    Task<Payment?> UpdateAsync(Payment payment);

    Task<bool> ExistsByRefAsync(string paymentRef, CancellationToken ct = default);
    Task<IReadOnlyList<Payment>> GetByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
}

public record AppointmentFilterDto(
    AppointmentStatus? Status,
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? DoctorId,
    int Page = 1,
    int PageSize = 20);

public record CenterSearchDto(
    string? City,
    string? Specialization,
    string? Name,
    int Page = 1,
    int PageSize = 20);

public record CenterConfigurationDto(
    int SlotDurationMinutes,
    int AdvanceBookingDays,
    int CancellationHours,
    bool AutoApproveAppointments,
    object? WorkingHours = null);

public record DoctorSearchDto(
    Guid? CenterId,
    string? Specialization,
    string? Name,
    DateOnly? AvailableOnDate,
    int Page = 1,
    int PageSize = 20);
