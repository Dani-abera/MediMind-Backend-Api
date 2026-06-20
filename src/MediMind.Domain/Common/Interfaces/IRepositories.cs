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
    Task ExecuteTransactionAsync(Func<Task> action, CancellationToken cancellationToken = default);
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
    Task<User?> GetByPhoneAndTypeAsync(string phone, UserType userType, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);
    Task<bool> ExistsByPhoneAsync(string phone, CancellationToken ct = default);
    Task<bool> ExistsByPhoneForRoleAsync(string phone, UserType userType, CancellationToken ct = default);
    Task<PagedResult<User>> SearchAsync(SuperAdminUserQueryDto query, CancellationToken ct = default);
    Task<bool> ExistsByEmailAsync(string email);
}

public interface IPatientRepository : IRepository<Patient>
{
    Task<Patient?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task<Patient?> GetWithHealthRecordsAsync(Guid patientId, CancellationToken ct = default);
    Task<Patient?> GetByPhoneAsync(string phone, CancellationToken ct = default);
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
    Task<PagedResult<Doctor>> GetAllAsync(SuperAdminDoctorQueryDto query, CancellationToken ct = default);
    Task<IReadOnlyList<Doctor>> GetUnverifiedAsync(CancellationToken ct = default);
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
    Task<int> GetChurnedCentersCountAsync(DateTime since, CancellationToken ct = default);
    Task<bool> ExistsByLicenseAsync(string licenseNumber, CancellationToken ct = default);
    Task<HealthcareCenter?> GetWithAdminsAsync(Guid centerId, CancellationToken ct = default);
    Task<PagedResult<HealthcareCenter>> GetAllAsync(SuperAdminCenterQueryDto query, CancellationToken ct = default);
    Task<IReadOnlyList<HealthcareCenter>> GetPendingApprovalAsync(CancellationToken ct = default);
    Task AddSubscriptionHistoryAsync(SubscriptionHistory history, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionHistory>> GetSubscriptionHistoryAsync(Guid centerId, CancellationToken ct = default);
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
    Task<IReadOnlyList<(DoctorSchedule Schedule, string CenterName)>> GetAllByDoctorAsync(Guid doctorId);
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
    Task<HealthTrendsResponseDto> GetTrendAsync(Guid patientId, int days);
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
    Task<IReadOnlyList<(Guid CenterId, string CenterName, decimal Revenue)>> GetPlatformRevenueByCenterAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
    Task<Dictionary<string, decimal>> GetPlatformRevenueByMonthAsync(DateOnly start, DateOnly end, CancellationToken ct = default);
}

public interface IVideoConsultationRepository : IRepository<VideoConsultation>
{
    Task<VideoConsultation?> GetByIdAsync(Guid consultationId);
    Task<VideoConsultation?> GetByAppointmentIdAsync(Guid appointmentId);
    Task<VideoConsultation?> GetByRoomIdAsync(string roomId);
    Task<VideoConsultation> CreateAsync(VideoConsultation consultation);
    Task<VideoConsultation?> UpdateStatusAsync(Guid consultationId, VideoConsultationStatus status);
    Task<VideoConsultationParticipant> AddParticipantAsync(VideoConsultationParticipant participant);
    Task UpdateParticipantLeftAsync(Guid consultationId, Guid userId);
    Task<IEnumerable<ChatMessage>> GetChatHistoryAsync(Guid consultationId, int page = 1, int pageSize = 50);
    Task<ChatMessage> SaveMessageAsync(ChatMessage message);
    Task SaveQualityMetricAsync(VideoQualityMetric metric);
    Task<IReadOnlyList<VideoConsultation>> GetByDoctorIdAsync(Guid doctorId, VideoConsultationStatus? status, bool todayOnly, int page, int pageSize);
}

public interface IPrescriptionRepository
{
    Task<Prescription?> GetByIdAsync(Guid prescriptionId, CancellationToken ct = default);
    Task<Prescription?> GetByIdWithDetailsAsync(Guid prescriptionId, CancellationToken ct = default);
    Task<Prescription?> GetByIdForUpdateAsync(Guid prescriptionId, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetByPatientAsync(Guid patientId, int page, int pageSize, CancellationToken ct = default);
    Task<IReadOnlyList<Prescription>> GetByDoctorAsync(Guid doctorId, int page, int pageSize, CancellationToken ct = default);
    Task<Prescription?> GetByAppointmentAsync(Guid appointmentId, CancellationToken ct = default);
    Task<Prescription> CreateAsync(Prescription prescription, CancellationToken ct = default);
    Task<Prescription?> UpdateStatusAsync(Guid prescriptionId, PrescriptionStatus status, CancellationToken ct = default);
    Task<Prescription?> UpdatePdfUrlAsync(Guid prescriptionId, string url, CancellationToken ct = default);
}

public interface IUserDeviceTokenRepository
{
    Task<IReadOnlyList<UserDeviceToken>> GetActiveTokensForUserAsync(Guid userId, CancellationToken ct = default);
    Task<UserDeviceToken?> FindByUserAndTokenAsync(Guid userId, string fcmToken, CancellationToken ct = default);
    Task UpsertAsync(UserDeviceToken token, CancellationToken ct = default);
    Task DeactivateAsync(Guid userId, string fcmToken, CancellationToken ct = default);
    Task DeactivateTokenStringAsync(string fcmToken, CancellationToken ct = default);
}

public interface INotificationLogRepository
{
    Task AddAsync(NotificationLog log, CancellationToken ct = default);
    Task<IReadOnlyList<NotificationLog>> GetRecentForUserAsync(Guid userId, int take, CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);
    Task<(IReadOnlyList<NotificationLog> Items, int TotalCount)> GetPagedAsync(Guid userId, int page, int pageSize, CancellationToken ct = default);
    Task<NotificationLog?> GetByIdForUserAsync(Guid id, Guid userId, CancellationToken ct = default);
    Task MarkAllReadAsync(Guid userId, CancellationToken ct = default);
}

public interface IMedicationReminderRepository
{
    Task<MedicationReminder?> GetByIdAsync(Guid id, Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationReminder>> GetByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<MedicationReminder>> GetAllActiveAsync(CancellationToken ct = default);
    Task AddAsync(MedicationReminder reminder, CancellationToken ct = default);
    Task DeleteAsync(MedicationReminder reminder, CancellationToken ct = default);
}

public interface IPatientMedicalHistoryRepository
{
    Task<PatientMedicalHistory?> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task AddAsync(PatientMedicalHistory history, CancellationToken ct = default);
    Task UpdateAsync(PatientMedicalHistory history, CancellationToken ct = default);
}

public interface IEmergencyContactRepository
{
    Task<IReadOnlyList<EmergencyContact>> GetByPatientIdAsync(Guid patientId, CancellationToken ct = default);
    Task<EmergencyContact?> GetByIdAsync(Guid contactId, Guid patientId, CancellationToken ct = default);
    Task<int> CountByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task AddAsync(EmergencyContact contact, CancellationToken ct = default);
    Task DeleteAsync(EmergencyContact contact, CancellationToken ct = default);
    Task ClearPrimaryAsync(Guid patientId, CancellationToken ct = default);
}

public interface IHealthRecordAttachmentRepository
{
    Task<IReadOnlyList<HealthRecordAttachment>> GetByRecordIdAsync(Guid healthRecordId, CancellationToken ct = default);
    Task<HealthRecordAttachment?> GetByIdAsync(Guid attachmentId, Guid healthRecordId, CancellationToken ct = default);
    Task AddAsync(HealthRecordAttachment attachment, CancellationToken ct = default);
    Task DeleteAsync(HealthRecordAttachment attachment, CancellationToken ct = default);
}

public interface IReviewRepository
{
    Task<IReadOnlyList<Review>> GetByDoctorIdAsync(Guid doctorId, int limit = 10, CancellationToken ct = default);
    Task<IReadOnlyList<Review>> GetByCenterIdAsync(Guid centerId, int limit = 10, CancellationToken ct = default);
    Task<bool> ExistsForAppointmentAsync(Guid appointmentId, bool isDoctor, CancellationToken ct = default);
    Task AddAsync(Review review, CancellationToken ct = default);
    Task<double> GetAverageRatingForDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<double> GetAverageRatingForCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<int> GetReviewCountForDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<int> GetReviewCountForCenterAsync(Guid centerId, CancellationToken ct = default);
}

public interface IFavoriteRepository
{
    Task<IReadOnlyList<Favorite>> GetDoctorFavoritesByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<Favorite>> GetCenterFavoritesByPatientAsync(Guid patientId, CancellationToken ct = default);
    Task<bool> IsDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default);
    Task<bool> IsCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default);
    Task<Favorite?> GetDoctorFavoriteAsync(Guid patientId, Guid doctorId, CancellationToken ct = default);
    Task<Favorite?> GetCenterFavoriteAsync(Guid patientId, Guid centerId, CancellationToken ct = default);
    Task AddAsync(Favorite favorite, CancellationToken ct = default);
    Task DeleteAsync(Favorite favorite, CancellationToken ct = default);
}

public interface INotificationPreferenceRepository
{
    Task<NotificationPreference?> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(NotificationPreference preference, CancellationToken ct = default);
    Task UpdateAsync(NotificationPreference preference, CancellationToken ct = default);
}

public interface IPrescriptionTemplateRepository
{
    Task<PrescriptionTemplate?> GetByIdAsync(Guid templateId, Guid doctorId, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionTemplate>> GetByDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<PrescriptionTemplate> CreateAsync(PrescriptionTemplate template, CancellationToken ct = default);
    Task<PrescriptionTemplate?> UpdateAsync(PrescriptionTemplate template, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid templateId, Guid doctorId, CancellationToken ct = default);
}

public interface IWaitlistSubscriptionRepository
{
    Task<WaitlistSubscription?> GetByIdAsync(Guid subscriptionId, Guid patientId, CancellationToken ct = default);
    Task<IReadOnlyList<WaitlistSubscription>> GetActiveByDoctorAndCenterAsync(Guid doctorId, Guid centerId, CancellationToken ct = default);
    Task<WaitlistSubscription?> GetActiveByPatientDoctorCenterAsync(Guid patientId, Guid doctorId, Guid centerId, CancellationToken ct = default);
    Task AddAsync(WaitlistSubscription subscription, CancellationToken ct = default);
    Task UpdateAsync(WaitlistSubscription subscription, CancellationToken ct = default);
}

public interface IAppointmentNoteRepository
{
    Task<AppointmentNote?> GetByAppointmentAsync(Guid appointmentId, Guid doctorId, CancellationToken ct = default);
    Task<AppointmentNote> CreateAsync(AppointmentNote note, CancellationToken ct = default);
    Task<AppointmentNote?> UpdateAsync(AppointmentNote note, CancellationToken ct = default);
}

public interface IAuditLogRepository
{
    Task AppendAsync(AuditLog entry, CancellationToken ct = default);
    Task<PagedResult<AuditLog>> QueryAsync(Guid centerId, AuditLogFilterDto filter, CancellationToken ct = default);
    Task<PagedResult<AuditLog>> QueryGlobalAsync(AuditLogFilterDto filter, CancellationToken ct = default);
}

public interface IDoctorInvitationRepository
{
    Task<DoctorInvitation?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task<DoctorInvitation?> GetByEmailAndCenterAsync(string email, Guid centerId, CancellationToken ct = default);
    Task<IReadOnlyList<DoctorInvitation>> GetPendingByCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<DoctorInvitation> CreateAsync(DoctorInvitation invitation, CancellationToken ct = default);
    Task UpdateAsync(DoctorInvitation invitation, CancellationToken ct = default);
}

public interface IScheduleExceptionRepository
{
    Task<IReadOnlyList<ScheduleException>> GetByDoctorAndCenterAsync(Guid doctorId, Guid centerId, CancellationToken ct = default);
    Task<IReadOnlyList<ScheduleException>> GetByDateRangeAsync(Guid doctorId, Guid centerId, DateOnly from, DateOnly to, CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid doctorId, Guid centerId, DateOnly date, CancellationToken ct = default);
    Task<ScheduleException> CreateAsync(ScheduleException exception, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid doctorId, Guid centerId, DateOnly date, CancellationToken ct = default);
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
    int PageSize = 20,
    DateOnly? AvailableOnDate = null);

public record CenterConfigurationDto(
    int SlotDurationMinutes,
    int AdvanceBookingDays,
    int CancellationHours,
    bool AutoApproveAppointments,
    Dictionary<string, string>? WorkingHours = null,
    bool RequiresPaymentBeforeConfirmation = false,
    List<string>? ServicesOffered = null);

public record DoctorSearchDto(
    Guid? CenterId,
    string? Specialization,
    string? Name,
    DateOnly? AvailableOnDate,
    int Page = 1,
    int PageSize = 20);

public record AuditLogFilterDto(
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? UserId,
    string? Action,
    int Page = 1,
    int PageSize = 50);

public record SuperAdminCenterQueryDto(
    string? Name,
    string? City,
    SubscriptionStatus? Status,
    int Page = 1,
    int PageSize = 20);

public record SuperAdminDoctorQueryDto(
    string? Name,
    string? Specialization,
    bool? LicenseVerified,
    int Page = 1,
    int PageSize = 20);

public record SuperAdminUserQueryDto(
    string? Search,
    UserType? UserType,
    UserStatus? Status,
    int Page = 1,
    int PageSize = 20);

public interface IPlatformConfigurationRepository
{
    Task<PlatformConfiguration?> GetAsync(CancellationToken ct = default);
    Task UpsertAsync(PlatformConfiguration config, CancellationToken ct = default);
}
