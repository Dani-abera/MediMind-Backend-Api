namespace MediMind.Application.Features.Admin;

// ─── Admin Account Management ─────────────────────────────────────────────────

public record AdminSummaryDto(
    Guid AdminId,
    string FullName,
    string Email,
    string PhoneNumber,
    string Role,
    bool IsActive,
    DateTime? LastLogin);

public record InviteAdminDto(
    string FullName,
    string Email,
    string PhoneNumber,
    string Role);

// ─── Patient Directory ────────────────────────────────────────────────────────

public record EnrolledPatientSummaryDto(
    Guid PatientId,
    string FullName,
    string PhoneNumber,
    DateOnly? LastVisit,
    int TotalAppointments);

public record EnrolledPatientDetailDto(
    Guid PatientId,
    string FullName,
    string Email,
    string PhoneNumber,
    DateOnly DateOfBirth,
    string Gender,
    string? Address,
    string? BloodType,
    string? Allergies,
    int TotalAppointments,
    DateOnly? LastVisit,
    DateOnly? FirstVisit,
    List<AppointmentSummaryDto> RecentAppointments);

public record AppointmentSummaryDto(
    Guid AppointmentId,
    DateOnly AppointmentDate,
    TimeOnly AppointmentTime,
    string DoctorName,
    string Status);

public record PatientDirectoryQueryDto(
    string? Search,
    int Page = 1,
    int PageSize = 20);

// ─── Today's Dashboard ────────────────────────────────────────────────────────

public record TodayDashboardDto(
    TodayAppointmentsDto Appointments,
    TodayQueueDto Queue,
    TodayRevenueDto Revenue,
    TodayDoctorsDto Doctors,
    PatientVolumeComparisonDto PatientVolumeVsLastWeek);

public record TodayAppointmentsDto(
    int Total,
    int Pending,
    int Confirmed,
    int Completed,
    int Cancelled,
    int NoShow);

public record TodayQueueDto(
    int CurrentlyWaiting,
    int InConsultation,
    double AverageWaitMinutes);

public record TodayRevenueDto(
    decimal TotalCollected,
    decimal Pending,
    string Currency = "ETB");

public record TodayDoctorsDto(
    int Working,
    int TotalRegistered);

public record PatientVolumeComparisonDto(
    int Today,
    int LastWeekSameDay,
    double ChangePercent);

// ─── Revenue Reporting ────────────────────────────────────────────────────────

public record RevenueQueryDto(
    DateOnly StartDate,
    DateOnly EndDate,
    string GroupBy = "day");

public record RevenueReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    decimal TotalPending,
    List<RevenueGroupDto> Groups);

public record RevenueGroupDto(
    string Label,
    decimal Collected,
    decimal Pending,
    int AppointmentCount);

public record RevenueCsvRowDto(
    string Date,
    Guid AppointmentId,
    string DoctorName,
    string PatientName,
    decimal Amount,
    string PaymentMethod,
    string Status);

// ─── Bulk Operations ──────────────────────────────────────────────────────────

public record BulkApproveDto(List<Guid> AppointmentIds);

public record BulkApproveResultDto(
    List<Guid> Approved,
    List<BulkApproveFailureDto> Failed);

public record BulkApproveFailureDto(Guid Id, string Reason);

// ─── Prescription Revoke ──────────────────────────────────────────────────────

public record RevokePrescriptionDto(string Reason);

// ─── Audit Log ────────────────────────────────────────────────────────────────

public record AuditLogQueryDto(
    DateOnly? StartDate,
    DateOnly? EndDate,
    Guid? UserId,
    string? Action,
    int Page = 1,
    int PageSize = 50);

public record AuditLogEntryDto(
    Guid LogId,
    DateTime Timestamp,
    Guid? UserId,
    string UserType,
    string Action,
    string? EntityType,
    Guid? EntityId,
    string IpAddress,
    string? Metadata);
