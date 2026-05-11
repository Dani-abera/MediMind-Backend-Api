using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;

namespace MediMind.Application.Features.SuperAdmin;

public interface ISuperAdminCenterService
{
    Task<PagedResult<SuperAdminCenterSummaryDto>> GetAllCentersAsync(SuperAdminCenterQueryDto query, CancellationToken ct = default);
    Task<IReadOnlyList<SuperAdminCenterSummaryDto>> GetPendingCentersAsync(CancellationToken ct = default);
    Task<SuperAdminCenterSummaryDto> GetCenterAsync(Guid centerId, CancellationToken ct = default);
    Task<SuperAdminCenterSummaryDto> ApproveCenterAsync(Guid centerId, ApproveCenterDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<SuperAdminCenterSummaryDto> RejectCenterAsync(Guid centerId, RejectCenterDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<SuperAdminCenterSummaryDto> SuspendCenterAsync(Guid centerId, SuspendCenterDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<SuperAdminCenterSummaryDto> ReactivateCenterAsync(Guid centerId, Guid superAdminId, CancellationToken ct = default);
    Task SoftDeleteCenterAsync(Guid centerId, Guid superAdminId, CancellationToken ct = default);
}

public interface ISuperAdminSubscriptionService
{
    Task<SubscriptionDetailDto> GetSubscriptionAsync(Guid centerId, CancellationToken ct = default);
    Task<SubscriptionDetailDto> UpdateSubscriptionAsync(Guid centerId, UpdateSubscriptionDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<SubscriptionDetailDto> ExtendSubscriptionAsync(Guid centerId, ExtendSubscriptionDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<IReadOnlyList<SubscriptionHistoryDto>> GetHistoryAsync(Guid centerId, CancellationToken ct = default);
    Task ApplyExpiredSubscriptionsAsync(CancellationToken ct = default);
}

public interface ISubscriptionPlanService
{
    Task<IReadOnlyList<SubscriptionPlanDto>> GetAllPlansAsync(bool includeInactive = false, CancellationToken ct = default);
    Task<SubscriptionPlanDto> GetPlanByIdAsync(Guid planId, CancellationToken ct = default);
    Task<SubscriptionPlanDto> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct = default);
    Task<SubscriptionPlanDto> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanDto dto, CancellationToken ct = default);
    Task DeactivatePlanAsync(Guid planId, CancellationToken ct = default);
}

public interface ISuperAdminDoctorService
{
    Task<PagedResult<SuperAdminDoctorSummaryDto>> GetAllDoctorsAsync(SuperAdminDoctorQueryDto query, CancellationToken ct = default);
    Task<IReadOnlyList<SuperAdminDoctorSummaryDto>> GetUnverifiedDoctorsAsync(CancellationToken ct = default);
    Task<SuperAdminDoctorSummaryDto> GetDoctorAsync(Guid doctorId, CancellationToken ct = default);
    Task<SuperAdminDoctorSummaryDto> VerifyLicenseAsync(Guid doctorId, VerifyDoctorLicenseDto dto, Guid superAdminId, CancellationToken ct = default);
    Task<SuperAdminDoctorSummaryDto> UnverifyLicenseAsync(Guid doctorId, UnverifyDoctorLicenseDto dto, Guid superAdminId, CancellationToken ct = default);
    Task SuspendDoctorAsync(Guid doctorId, string reason, Guid superAdminId, CancellationToken ct = default);
    Task ReactivateDoctorAsync(Guid doctorId, Guid superAdminId, CancellationToken ct = default);
}

public interface ISuperAdminUserService
{
    Task<PagedResult<SuperAdminUserSummaryDto>> SearchUsersAsync(SuperAdminUserQueryDto query, CancellationToken ct = default);
    Task<SuperAdminUserSummaryDto> GetUserAsync(Guid userId, CancellationToken ct = default);
    Task SuspendUserAsync(Guid userId, SuspendUserDto dto, Guid superAdminId, CancellationToken ct = default);
    Task ReactivateUserAsync(Guid userId, Guid superAdminId, CancellationToken ct = default);
    Task ForceLogoutAsync(Guid userId, ForceLogoutDto dto, Guid superAdminId, CancellationToken ct = default);
    Task SoftDeleteUserAsync(Guid userId, SoftDeleteUserDto dto, Guid superAdminId, CancellationToken ct = default);
}

public interface ISuperAdminPlatformService
{
    Task<PlatformKpiDto> GetKpisAsync(CancellationToken ct = default);
    Task<PlatformRevenueReportDto> GetRevenueReportAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);
    Task<IReadOnlyList<PlatformRevenueCsvRowDto>> GetRevenueCsvRowsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default);
}
