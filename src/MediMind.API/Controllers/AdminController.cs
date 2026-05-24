using CsvHelper;
using MediMind.Application.Features.Admin;
using MediMind.Application.Features.Auth;
using MediMind.Application.Features.CenterManagement;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace MediMind.API.Controllers;

/// <summary>
/// Admin-scoped operations: account management, patient directory,
/// today's dashboard, revenue, bulk operations, audit log.
/// All endpoints verify center_id claim == route {id} via service layer.
/// </summary>
[ApiController]
[Route("api/v1/healthcare-centers")]
[Authorize(Policy = "AdminOnly")]
public class AdminController(
    IAdminManagementService adminManagement,
    IAdminAuthService adminAuth,
    IPatientDirectoryService patientDirectory,
    ITodayDashboardService todayDashboard,
    IRevenueService revenueService,
    IAuditLogService auditLog,
    ICurrentUser currentUser) : ControllerBase
{
    // ═══════════════════════════════════════════════════════════════════════════
    // ADMIN ACCOUNT MANAGEMENT
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>List all admin accounts at this center.</summary>
    [Tags("Admin — Account Management")]
    [HttpGet("{id:guid}/admins")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListAdmins(Guid id, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await adminManagement.ListAdminsAsync(id, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Invite another admin to this center. Sends invitation email with registration link.</summary>
    [Tags("Admin — Account Management")]
    [HttpPost("{id:guid}/admins")]
    [ProducesResponseType(typeof(AdminSummaryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> InviteAdmin(Guid id, [FromBody] InviteAdminDto dto, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await adminManagement.InviteAdminAsync(id, dto, currentUser.UserId, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Remove an admin from this center (cannot remove self or last admin).</summary>
    [Tags("Admin — Account Management")]
    [HttpDelete("{id:guid}/admins/{adminId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RemoveAdmin(Guid id, Guid adminId, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        await adminManagement.RemoveAdminAsync(id, adminId, currentUser.UserId, ct);
        return NoContent();
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // PATIENT DIRECTORY
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>List patients enrolled at this center (have ≥1 appointment here).</summary>
    [Tags("Admin — Patient Directory")]
    [HttpGet("{id:guid}/patients")]
    [ProducesResponseType(typeof(PagedResult<EnrolledPatientSummaryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ListPatients(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        EnsureCenterAccess(id);
        var result = await patientDirectory.ListEnrolledPatientsAsync(
            id, new PatientDirectoryQueryDto(search, page, pageSize), currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Get patient records for an enrolled patient (FR-023).</summary>
    [Tags("Admin — Patient Directory")]
    [HttpGet("{id:guid}/patients/{patientId:guid}")]
    [ProducesResponseType(typeof(EnrolledPatientDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPatient(Guid id, Guid patientId, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await patientDirectory.GetEnrolledPatientAsync(id, patientId, currentUser.UserId, ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // TODAY'S DASHBOARD
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Get today's operational dashboard snapshot for this center.</summary>
    [Tags("Admin — Dashboard")]
    [HttpGet("{id:guid}/dashboard/today")]
    [ProducesResponseType(typeof(TodayDashboardDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> TodayDashboard(Guid id, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await todayDashboard.GetTodayDashboardAsync(id, currentUser.UserId, ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // REVENUE REPORTING
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Aggregated revenue report with payment status breakdown.</summary>
    [Tags("Admin — Revenue")]
    [HttpGet("{id:guid}/revenue")]
    [ProducesResponseType(typeof(RevenueReportDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Revenue(
        Guid id,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        [FromQuery] string groupBy = "day",
        CancellationToken ct = default)
    {
        EnsureCenterAccess(id);
        var result = await revenueService.GetRevenueReportAsync(
            id, new RevenueQueryDto(startDate, endDate, groupBy), currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Export revenue as CSV: Date, AppointmentId, DoctorName, PatientName, Amount, PaymentMethod, Status.</summary>
    [Tags("Admin — Revenue")]
    [HttpGet("{id:guid}/revenue/export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> RevenueExportCsv(
        Guid id,
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var rows = await revenueService.GetRevenueCsvRowsAsync(id, startDate, endDate, currentUser.UserId, ct);

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(rows, ct);
        }

        return File(stream.ToArray(), "text/csv",
            $"revenue-{id}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // AUDIT LOG
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Query the append-only audit log for this center (NFR-008).</summary>
    [Tags("Admin — Audit")]
    [HttpGet("{id:guid}/audit-log")]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AuditLog(
        Guid id,
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        EnsureCenterAccess(id);
        var result = await auditLog.GetAuditLogsAsync(
            id,
            new AuditLogQueryDto(startDate, endDate, userId, action, page, pageSize),
            currentUser.UserId,
            ct);
        return Ok(result);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // CENTER LOGO (replaces /api/v1/profile-images/healthcare-centers/{id})
    // ═══════════════════════════════════════════════════════════════════════════

    // Logo endpoints are in CenterLogoController.cs to avoid route conflicts with HealthcareCentersController

    // ═══════════════════════════════════════════════════════════════════════════
    // DOCTOR INVITATIONS (FR-020)
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>Invite a doctor to join this center. Sends an invitation email valid for 48 hours (FR-020).</summary>
    [Tags("Admin — Doctors")]
    [HttpPost("{id:guid}/doctors/invite")]
    [ProducesResponseType(typeof(DoctorInvitationResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> InviteDoctor(Guid id, [FromBody] InviteDoctorRequest dto, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await adminAuth.InviteDoctorAsync(id, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Create a doctor account immediately and link them to this center. Account is live at once; doctor receives badge number via SMS and email. No activation required.</summary>
    [Tags("Admin — Doctors")]
    [HttpPost("{id:guid}/doctors/create")]
    [ProducesResponseType(typeof(DoctorCreatedResult), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateDoctor(Guid id, [FromBody] CreateDoctorRequest dto, CancellationToken ct)
    {
        EnsureCenterAccess(id);
        var result = await adminAuth.CreateDoctorAsync(dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    // ─── Helper ──────────────────────────────────────────────────────────────

    private void EnsureCenterAccess(Guid centerId)
    {
        if (currentUser.TenantId.HasValue && currentUser.TenantId.Value != centerId)
            throw new Domain.Exceptions.TenantIsolationException();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// BULK OPERATIONS (separate route prefix: /api/v1/appointments and /api/v1/queue)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Admin bulk operations on appointments.</summary>
[ApiController]
[Route("api/v1/appointments")]
[Authorize(Policy = "AdminOnly")]
public class AppointmentBulkController(
    IBulkOperationsService bulkOps,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Bulk-approve multiple pending appointments (verifies each belongs to admin's center).</summary>
    [Tags("Admin — Appointments")]
    [HttpPost("bulk-approve")]
    [ProducesResponseType(typeof(BulkApproveResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApproveDto dto, CancellationToken ct)
    {
        if (dto.AppointmentIds is null || dto.AppointmentIds.Count == 0)
            return BadRequest(new { error = "AppointmentIds list cannot be empty." });

        var centerTenantId = currentUser.TenantId
            ?? throw new Domain.Exceptions.ForbiddenException("Admin must have a center assigned.");

        var result = await bulkOps.BulkApproveAppointmentsAsync(dto, currentUser.UserId, centerTenantId, ct);
        return Ok(result);
    }
}

/// <summary>Queue skip operation for admin.</summary>
[ApiController]
[Route("api/v1/queue")]
[Authorize(Policy = "AdminOnly")]
public class QueueSkipController(
    IBulkOperationsService bulkOps,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Move a queue entry to the end (patient not ready). Sends notification.</summary>
    [Tags("Admin — Queue")]
    [HttpPost("{queueId:guid}/skip")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Skip(Guid queueId, CancellationToken ct)
    {
        var centerTenantId = currentUser.TenantId
            ?? throw new Domain.Exceptions.ForbiddenException("Admin must have a center assigned.");
        await bulkOps.SkipQueueEntryAsync(queueId, currentUser.UserId, centerTenantId, ct);
        return NoContent();
    }
}
