using MediMind.Domain.Common.Interfaces;

namespace MediMind.Application.Features.Admin;

public interface IAdminManagementService
{
    Task<IReadOnlyList<AdminSummaryDto>> ListAdminsAsync(Guid centerId, Guid requesterId, CancellationToken ct = default);
    Task<AdminSummaryDto> InviteAdminAsync(Guid centerId, InviteAdminDto dto, Guid requesterId, CancellationToken ct = default);
    Task RemoveAdminAsync(Guid centerId, Guid adminId, Guid requesterId, CancellationToken ct = default);
}

public interface IPatientDirectoryService
{
    Task<PagedResult<EnrolledPatientSummaryDto>> ListEnrolledPatientsAsync(Guid centerId, PatientDirectoryQueryDto query, Guid requesterId, CancellationToken ct = default);
    Task<EnrolledPatientDetailDto> GetEnrolledPatientAsync(Guid centerId, Guid patientId, Guid requesterId, CancellationToken ct = default);
}

public interface ITodayDashboardService
{
    Task<TodayDashboardDto> GetTodayDashboardAsync(Guid centerId, Guid requesterId, CancellationToken ct = default);
}

public interface IRevenueService
{
    Task<RevenueReportDto> GetRevenueReportAsync(Guid centerId, RevenueQueryDto query, Guid requesterId, CancellationToken ct = default);
    Task<IEnumerable<RevenueCsvRowDto>> GetRevenueCsvRowsAsync(Guid centerId, DateOnly startDate, DateOnly endDate, Guid requesterId, CancellationToken ct = default);
}

public interface IBulkOperationsService
{
    Task<BulkApproveResultDto> BulkApproveAppointmentsAsync(BulkApproveDto dto, Guid requesterId, Guid centerTenantId, CancellationToken ct = default);
    Task SkipQueueEntryAsync(Guid queueId, Guid requesterId, Guid centerTenantId, CancellationToken ct = default);
}

public interface IAuditLogger
{
    Task LogAsync(
        string action,
        Guid? userId,
        string userType,
        Guid? centerId,
        string? entityType = null,
        Guid? entityId = null,
        string? metadata = null,
        CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task<PagedResult<AuditLogEntryDto>> GetAuditLogsAsync(Guid centerId, AuditLogQueryDto query, Guid requesterId, CancellationToken ct = default);
    Task<PagedResult<AuditLogEntryDto>> GetGlobalAuditLogsAsync(AuditLogQueryDto query, CancellationToken ct = default);
}
