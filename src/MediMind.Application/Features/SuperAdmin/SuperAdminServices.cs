using MediMind.Application.Features.Admin;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.Logging;

// UserType is used in SuperAdminPlatformService
using UserType = MediMind.Domain.Enums.UserType;

namespace MediMind.Application.Features.SuperAdmin;

// ─── Center Service ────────────────────────────────────────────────────────────

public class SuperAdminCenterService(
    IHealthcareCenterRepository centerRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger,
    ILogger<SuperAdminCenterService> logger) : ISuperAdminCenterService
{
    public async Task<PagedResult<SuperAdminCenterSummaryDto>> GetAllCentersAsync(SuperAdminCenterQueryDto query, CancellationToken ct = default)
    {
        var result = await centerRepository.GetAllAsync(query, ct);
        var items = await MapManyAsync(result.Items, ct);
        return new PagedResult<SuperAdminCenterSummaryDto>(items, result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<IReadOnlyList<SuperAdminCenterSummaryDto>> GetPendingCentersAsync(CancellationToken ct = default)
    {
        var centers = await centerRepository.GetPendingApprovalAsync(ct);
        return await MapManyAsync(centers, ct);
    }

    public async Task<SuperAdminCenterSummaryDto> GetCenterAsync(Guid centerId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        return await MapAsync(center, ct);
    }

    public async Task<SuperAdminCenterSummaryDto> ApproveCenterAsync(Guid centerId, ApproveCenterDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        if (center.SubscriptionStatus != SubscriptionStatus.PendingApproval)
            throw new DomainException("Only centers with PendingApproval status can be approved.");

        var oldStatus = center.SubscriptionStatus;
        center.ApproveTrial();

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Trial, superAdminId, "Trial", notes: dto.Notes);
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_CenterApproved, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, $"{{\"notes\":\"{dto.Notes}\"}}", ct);
        logger.LogInformation("Center {CenterId} approved by SuperAdmin {SuperAdminId}", centerId, superAdminId);
        return await MapAsync(center, ct);
    }

    public async Task<SuperAdminCenterSummaryDto> RejectCenterAsync(Guid centerId, RejectCenterDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var oldStatus = center.SubscriptionStatus;
        center.Reject(dto.Reason);

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Rejected, superAdminId, notes: dto.Reason);
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_CenterRejected, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
        return await MapAsync(center, ct);
    }

    public async Task<SuperAdminCenterSummaryDto> SuspendCenterAsync(Guid centerId, SuspendCenterDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var oldStatus = center.SubscriptionStatus;
        center.Suspend(dto.Reason);

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Suspended, superAdminId, notes: dto.Reason);
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_CenterSuspended, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
        return await MapAsync(center, ct);
    }

    public async Task<SuperAdminCenterSummaryDto> ReactivateCenterAsync(Guid centerId, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var oldStatus = center.SubscriptionStatus;
        center.Reactivate();

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Active, superAdminId);
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_CenterReactivated, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, null, ct);
        return await MapAsync(center, ct);
    }

    public async Task SoftDeleteCenterAsync(Guid centerId, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        center.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_CenterDeleted, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, null, ct);
    }

    private async Task<List<SuperAdminCenterSummaryDto>> MapManyAsync(IReadOnlyList<HealthcareCenter> centers, CancellationToken ct)
    {
        var result = new List<SuperAdminCenterSummaryDto>(centers.Count);
        foreach (var c in centers)
            result.Add(await MapAsync(c, ct));
        return result;
    }

    private async Task<SuperAdminCenterSummaryDto> MapAsync(HealthcareCenter c, CancellationToken ct)
    {
        var doctorCount = (await centerRepository.GetDoctorsAsync(c.Id)).Count(x => x.IsActive);
        return new SuperAdminCenterSummaryDto(
            c.Id, c.CenterName, c.CenterType, c.LicenseNumber,
            c.City, c.Region, c.PhoneNumber, c.Email,
            c.SubscriptionStatus.ToString(), c.SubscriptionEndDate,
            c.RejectionReason, c.IsDeleted, doctorCount, c.CreatedAt);
    }
}

// ─── Subscription Service ──────────────────────────────────────────────────────

public class SuperAdminSubscriptionService(
    IHealthcareCenterRepository centerRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger) : ISuperAdminSubscriptionService
{
    public async Task<SubscriptionDetailDto> GetSubscriptionAsync(Guid centerId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        var history = await centerRepository.GetSubscriptionHistoryAsync(centerId, ct);
        return MapDetail(center, history);
    }

    public async Task<SubscriptionDetailDto> UpdateSubscriptionAsync(Guid centerId, UpdateSubscriptionDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var oldStatus = center.SubscriptionStatus;
        center.ApproveSubscription(dto.StartDate, dto.EndDate);

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Active, superAdminId, dto.Plan, dto.StartDate, dto.EndDate, dto.Notes);
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_SubscriptionUpdated, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, $"{{\"plan\":\"{dto.Plan}\"}}", ct);

        var updatedHistory = await centerRepository.GetSubscriptionHistoryAsync(centerId, ct);
        return MapDetail(center, updatedHistory);
    }

    public async Task<SubscriptionDetailDto> ExtendSubscriptionAsync(Guid centerId, ExtendSubscriptionDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var center = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);

        var newEnd = (center.SubscriptionEndDate ?? DateOnly.FromDateTime(DateTime.UtcNow)).AddDays(dto.Days);
        var oldStatus = center.SubscriptionStatus;
        center.ApproveSubscription(center.SubscriptionStartDate ?? DateOnly.FromDateTime(DateTime.UtcNow), newEnd);

        var history = SubscriptionHistory.Create(centerId, oldStatus, SubscriptionStatus.Active, superAdminId, notes: $"Extended by {dto.Days} days. {dto.Notes}".Trim());
        await centerRepository.AddSubscriptionHistoryAsync(history, ct);
        await unitOfWork.SaveChangesAsync(ct);

        await auditLogger.LogAsync(AuditActions.SuperAdmin_SubscriptionExtended, superAdminId, "SuperAdmin", centerId, "HealthcareCenter", centerId, $"{{\"days\":{dto.Days}}}", ct);

        var updatedHistory = await centerRepository.GetSubscriptionHistoryAsync(centerId, ct);
        return MapDetail(center, updatedHistory);
    }

    public async Task<IReadOnlyList<SubscriptionHistoryDto>> GetHistoryAsync(Guid centerId, CancellationToken ct = default)
    {
        _ = await centerRepository.GetByIdAsync(centerId)
            ?? throw new NotFoundException(nameof(HealthcareCenter), centerId);
        var history = await centerRepository.GetSubscriptionHistoryAsync(centerId, ct);
        return history.Select(MapHistoryItem).ToList();
    }

    private static SubscriptionDetailDto MapDetail(HealthcareCenter center, IReadOnlyList<SubscriptionHistory> history) =>
        new(
            center.Id,
            center.CenterName,
            center.SubscriptionStatus == SubscriptionStatus.Active ? "Standard" : center.SubscriptionStatus.ToString(),
            center.SubscriptionStatus.ToString(),
            center.SubscriptionStartDate,
            center.SubscriptionEndDate,
            center.IsSubscriptionActive,
            history.Select(MapHistoryItem).ToList());

    private static SubscriptionHistoryDto MapHistoryItem(SubscriptionHistory h) =>
        new(h.Id, h.OldStatus, h.NewStatus, h.Plan, h.StartDate, h.EndDate, h.Notes, h.ChangedBy, h.ChangedAt);
}

// ─── Doctor Service ────────────────────────────────────────────────────────────

public class SuperAdminDoctorService(
    IDoctorRepository doctorRepository,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger) : ISuperAdminDoctorService
{
    public async Task<PagedResult<SuperAdminDoctorSummaryDto>> GetAllDoctorsAsync(SuperAdminDoctorQueryDto query, CancellationToken ct = default)
    {
        var result = await doctorRepository.GetAllAsync(query, ct);
        return new PagedResult<SuperAdminDoctorSummaryDto>(result.Items.Select(MapDoctor).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<IReadOnlyList<SuperAdminDoctorSummaryDto>> GetUnverifiedDoctorsAsync(CancellationToken ct = default)
    {
        var doctors = await doctorRepository.GetUnverifiedAsync(ct);
        return doctors.Select(MapDoctor).ToList();
    }

    public async Task<SuperAdminDoctorSummaryDto> GetDoctorAsync(Guid doctorId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);
        return MapDoctor(doctor);
    }

    public async Task<SuperAdminDoctorSummaryDto> VerifyLicenseAsync(Guid doctorId, VerifyDoctorLicenseDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        doctor.VerifyLicense(dto.Notes);
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_DoctorLicenseVerified, superAdminId, "SuperAdmin", null, "Doctor", doctorId, $"{{\"notes\":\"{dto.Notes}\"}}", ct);
        return MapDoctor(doctor);
    }

    public async Task<SuperAdminDoctorSummaryDto> UnverifyLicenseAsync(Guid doctorId, UnverifyDoctorLicenseDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        doctor.UnverifyLicense(dto.Reason);
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_DoctorLicenseRevoked, superAdminId, "SuperAdmin", null, "Doctor", doctorId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
        return MapDoctor(doctor);
    }

    public async Task SuspendDoctorAsync(Guid doctorId, string reason, Guid superAdminId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        doctor.Suspend(reason);
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserSuspended, superAdminId, "SuperAdmin", null, "Doctor", doctorId, $"{{\"reason\":\"{reason}\"}}", ct);
    }

    public async Task ReactivateDoctorAsync(Guid doctorId, Guid superAdminId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        doctor.Reactivate();
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserReactivated, superAdminId, "SuperAdmin", null, "Doctor", doctorId, null, ct);
    }

    private static SuperAdminDoctorSummaryDto MapDoctor(Doctor d) =>
        new(
            d.Id,
            d.FullName,
            d.BadgeNumber,
            d.Specialization,
            d.LicenseNumber,
            d.LicenseVerified,
            d.LicenseVerificationNotes,
            d.Status.ToString(),
            d.DoctorHealthcareCenters
                .Where(x => x.IsActive)
                .Select(x => x.Center?.CenterName ?? x.CenterId.ToString())
                .ToList(),
            d.CreatedAt);
}

// ─── User Service ──────────────────────────────────────────────────────────────

public class SuperAdminUserService(
    IUserRepository userRepository,
    ITokenService tokenService,
    IUnitOfWork unitOfWork,
    IAuditLogger auditLogger) : ISuperAdminUserService
{
    public async Task<PagedResult<SuperAdminUserSummaryDto>> SearchUsersAsync(SuperAdminUserQueryDto query, CancellationToken ct = default)
    {
        var result = await userRepository.SearchAsync(query, ct);
        return new PagedResult<SuperAdminUserSummaryDto>(result.Items.Select(MapUser).ToList(), result.Page, result.PageSize, result.TotalCount);
    }

    public async Task<SuperAdminUserSummaryDto> GetUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);
        return MapUser(user);
    }

    public async Task SuspendUserAsync(Guid userId, SuspendUserDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        user.Suspend(dto.Reason);
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserSuspended, superAdminId, "SuperAdmin", null, "User", userId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
    }

    public async Task ReactivateUserAsync(Guid userId, Guid superAdminId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        user.Reactivate();
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserReactivated, superAdminId, "SuperAdmin", null, "User", userId, null, ct);
    }

    public async Task ForceLogoutAsync(Guid userId, ForceLogoutDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        tokenService.RevokeRefreshToken(userId);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserForceLogout, superAdminId, "SuperAdmin", null, "User", userId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
    }

    public async Task SoftDeleteUserAsync(Guid userId, SoftDeleteUserDto dto, Guid superAdminId, CancellationToken ct = default)
    {
        var user = await userRepository.GetByIdAsync(userId, ct)
            ?? throw new NotFoundException(nameof(User), userId);

        user.SoftDelete();
        await unitOfWork.SaveChangesAsync(ct);
        await auditLogger.LogAsync(AuditActions.SuperAdmin_UserDeleted, superAdminId, "SuperAdmin", null, "User", userId, $"{{\"reason\":\"{dto.Reason}\"}}", ct);
    }

    private static SuperAdminUserSummaryDto MapUser(User u) =>
        new(u.Id, u.FullName, u.Email, u.PhoneNumber,
            u.UserType.ToString(), u.Status.ToString(),
            u.IsDeleted, u.LastLogin, u.CreatedAt);
}

// ─── Platform Service ──────────────────────────────────────────────────────────

public class SuperAdminPlatformService(
    IHealthcareCenterRepository centerRepository,
    IDoctorRepository doctorRepository,
    IUserRepository userRepository,
    IAppointmentRepository appointmentRepository,
    IPaymentRepository paymentRepository) : ISuperAdminPlatformService
{
    public async Task<PlatformKpiDto> GetKpisAsync(CancellationToken ct = default)
    {
        var allCenters = await centerRepository.GetAllAsync(new SuperAdminCenterQueryDto(null, null, null, 1, 10000), ct);
        var totalCenters = allCenters.TotalCount;
        var pendingCenters = allCenters.Items.Count(c => c.SubscriptionStatus == SubscriptionStatus.PendingApproval);
        var activeCenters = allCenters.Items.Count(c => c.SubscriptionStatus == SubscriptionStatus.Active || c.SubscriptionStatus == SubscriptionStatus.Trial);
        var suspendedCenters = allCenters.Items.Count(c => c.SubscriptionStatus == SubscriptionStatus.Suspended);

        var allDoctors = await doctorRepository.GetAllAsync(new SuperAdminDoctorQueryDto(null, null, null, 1, 100000), ct);
        var totalDoctors = allDoctors.TotalCount;
        var verifiedDoctors = allDoctors.Items.Count(d => d.LicenseVerified);

        var allUsers = await userRepository.SearchAsync(new SuperAdminUserQueryDto(null, null, null, 1, 100000), ct);
        var totalPatients = allUsers.Items.Count(u => u.UserType == UserType.Patient);
        var activeUsers = allUsers.Items.Count(u => u.LastLogin.HasValue && u.LastLogin.Value >= DateTime.UtcNow.AddDays(-30));

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayFilter = new AppointmentFilterDto(null, today, today, null, 1, 100000);
        var totalApptToday = 0;
        foreach (var center in allCenters.Items.Where(c => !c.IsDeleted))
        {
            var appts = await appointmentRepository.GetByCenterAsync(center.Id, todayFilter);
            totalApptToday += appts.TotalCount;
        }

        var monthStart = DateOnly.FromDateTime(new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1));
        var totalRevenue = 0m;
        foreach (var center in allCenters.Items.Where(c => !c.IsDeleted))
            totalRevenue += await paymentRepository.GetTotalRevenueAsync(center.Id, monthStart, today);

        return new PlatformKpiDto(
            totalCenters, pendingCenters, activeCenters, suspendedCenters,
            totalDoctors, verifiedDoctors, totalDoctors - verifiedDoctors,
            totalPatients, totalApptToday, totalRevenue, activeUsers);
    }

    public async Task<PlatformRevenueReportDto> GetRevenueReportAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var rows = await GetRevenueCsvRowsAsync(startDate, endDate, ct);
        var byDay = rows
            .GroupBy(r => r.Date)
            .Select(g => new PlatformRevenueSummaryDto(g.Key, g.Sum(x => x.DailyRevenue), g.Sum(x => x.AppointmentCount)))
            .OrderBy(x => x.Date)
            .ToList();
        return new PlatformRevenueReportDto(startDate, endDate, rows.Sum(x => x.DailyRevenue), byDay);
    }

    public async Task<IReadOnlyList<PlatformRevenueCsvRowDto>> GetRevenueCsvRowsAsync(DateOnly startDate, DateOnly endDate, CancellationToken ct = default)
    {
        var allCenters = await centerRepository.GetAllAsync(new SuperAdminCenterQueryDto(null, null, null, 1, 10000), ct);
        var rows = new List<PlatformRevenueCsvRowDto>();

        foreach (var center in allCenters.Items.Where(c => !c.IsDeleted))
        {
            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var filter = new AppointmentFilterDto(null, d, d, null, 1, 10000);
                var appts = await appointmentRepository.GetByCenterAsync(center.Id, filter);
                if (appts.TotalCount == 0) continue;

                var rev = await paymentRepository.GetTotalRevenueAsync(center.Id, d, d);
                rows.Add(new PlatformRevenueCsvRowDto(d, center.Id, center.CenterName, rev, appts.TotalCount));
            }
        }

        return rows.OrderBy(r => r.Date).ThenBy(r => r.CenterName).ToList();
    }
}
