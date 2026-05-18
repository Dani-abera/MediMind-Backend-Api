using MediMind.Domain.Enums;

namespace MediMind.Application.Features.SuperAdmin;

// ─── Center Management ────────────────────────────────────────────────────────

public record SuperAdminCenterSummaryDto(
    Guid Id,
    string CenterName,
    string CenterType,
    string LicenseNumber,
    string City,
    string Region,
    string PhoneNumber,
    string Email,
    string SubscriptionStatus,
    DateOnly? SubscriptionEndDate,
    string? RejectionReason,
    bool IsDeleted,
    int DoctorCount,
    DateTime CreatedAt);

public record ApproveCenterDto(string? Notes);

public record RejectCenterDto(string Reason);

public record SuspendCenterDto(string Reason);

public record UpdateSubscriptionDto(
    string Plan,
    DateOnly StartDate,
    DateOnly EndDate,
    string? Notes);

public record ExtendSubscriptionDto(int Days, string? Notes);

public record SubscriptionHistoryDto(
    Guid Id,
    SubscriptionStatus OldStatus,
    SubscriptionStatus NewStatus,
    string? Plan,
    DateOnly? StartDate,
    DateOnly? EndDate,
    string? Notes,
    Guid ChangedBy,
    DateTime ChangedAt);

public record SubscriptionDetailDto(
    Guid CenterId,
    string CenterName,
    string CurrentPlan,
    string SubscriptionStatus,
    DateOnly? StartDate,
    DateOnly? EndDate,
    bool IsActive,
    IReadOnlyList<SubscriptionHistoryDto> History);

// ─── Doctor Identity Management ───────────────────────────────────────────────

public record SuperAdminDoctorSummaryDto(
    Guid Id,
    string FullName,
    string BadgeNumber,
    string Specialization,
    string LicenseNumber,
    bool LicenseVerified,
    string? LicenseVerificationNotes,
    string Status,
    IReadOnlyList<string> Centers,
    DateTime CreatedAt);

public record VerifyDoctorLicenseDto(string? Notes);

public record UnverifyDoctorLicenseDto(string Reason);

// ─── User Management ──────────────────────────────────────────────────────────

public record SuperAdminUserSummaryDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string UserType,
    string Status,
    bool IsDeleted,
    DateTime? LastLogin,
    DateTime CreatedAt);

public record SuspendUserDto(string Reason);

public record ForceLogoutDto(string Reason);

public record SoftDeleteUserDto(string Reason);

// ─── Platform Analytics ───────────────────────────────────────────────────────

public record PlatformKpiDto(
    int TotalCenters,
    int PendingApprovalCenters,
    int ActiveCenters,
    int SuspendedCenters,
    int TotalDoctors,
    int VerifiedDoctors,
    int UnverifiedDoctors,
    int TotalPatients,
    int TotalAppointmentsToday,
    decimal TotalRevenueThisMonth,
    int ActiveUsersLast30Days);

public record PlatformRevenueSummaryDto(
    DateOnly Date,
    decimal Amount,
    int AppointmentCount);

public record PlatformRevenueReportDto(
    DateOnly StartDate,
    DateOnly EndDate,
    decimal TotalRevenue,
    IReadOnlyList<PlatformRevenueSummaryDto> ByDay);

public record PlatformRevenueCsvRowDto(
    DateOnly Date,
    Guid CenterId,
    string CenterName,
    decimal DailyRevenue,
    int AppointmentCount);

public record RevenueByCenterDto(Guid CenterId, string CenterName, decimal RevenueEtb);

public record PlatformAnalyticsDto(
    decimal TotalRevenueEtb,
    IReadOnlyList<RevenueByCenterDto> RevenueByCenterTop20,
    Dictionary<string, decimal> RevenueByMonth,
    Dictionary<string, int> NewCentersPerMonth,
    Dictionary<string, int> NewDoctorsPerMonth,
    Dictionary<string, int> NewPatientsPerMonth,
    double ChurnRate30d,
    double ChurnRate60d,
    double ChurnRate90d);

// ─── Queue Regeneration ───────────────────────────────────────────────────────

public record RegenerateQueueDto(string ConfirmationMessage, DateOnly? Date);

// ─── Subscription Plans ───────────────────────────────────────────────────────

public record SubscriptionPlanDto(
    Guid PlanId,
    string Name,
    SubscriptionPlanTier Tier,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxDoctors,
    int MaxAppointmentsPerDay,
    List<string> Features,
    string? Description,
    bool IsActive,
    DateTime CreatedAt);

public record CreateSubscriptionPlanDto(
    string Name,
    SubscriptionPlanTier Tier,
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxDoctors,
    int MaxAppointmentsPerDay,
    List<string> Features,
    string? Description);

public record UpdateSubscriptionPlanDto(
    decimal MonthlyPrice,
    decimal YearlyPrice,
    int MaxDoctors,
    int MaxAppointmentsPerDay,
    List<string>? Features,
    string? Description);

// ─── SuperAdmin Profile ───────────────────────────────────────────────────────

public record SuperAdminProfileDto(
    Guid Id,
    string FullName,
    string Email,
    string PhoneNumber,
    string Gender,
    DateOnly DateOfBirth,
    string? ProfileImageUrl,
    DateTime? LastLogin,
    bool IsVerified,
    DateTime CreatedAt);

public record UpdateSuperAdminProfileDto(
    string FullName,
    DateOnly DateOfBirth,
    string Gender);

// ─── Platform Settings ────────────────────────────────────────────────────────

public record PlatformSettingsDto(
    double TrialFeeEtb,
    double BasicFeeEtb,
    double PremiumFeeEtb,
    int MaxAdvanceBookingDays,
    int MaxSlotDurationMinutes,
    Dictionary<string, bool> FeatureFlags,
    bool MaintenanceMode,
    string MaintenanceMessage);

public record UpdatePlatformSettingsDto(
    double TrialFeeEtb,
    double BasicFeeEtb,
    double PremiumFeeEtb,
    int MaxAdvanceBookingDays,
    int MaxSlotDurationMinutes,
    Dictionary<string, bool> FeatureFlags,
    bool MaintenanceMode,
    string MaintenanceMessage);
