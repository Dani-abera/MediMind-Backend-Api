using CsvHelper;
using MediMind.Application.Features.SuperAdmin;
using MediMind.Application.Features.Admin;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using System.Text;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/platform")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Platform Analytics")]
public class SuperAdminPlatformController(
    ISuperAdminPlatformService platformService) : ControllerBase
{
    /// <summary>Get platform-wide KPI dashboard snapshot.</summary>
    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(PlatformKpiDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var result = await platformService.GetKpisAsync(ct);
        return Ok(result);
    }

    /// <summary>Get platform-wide revenue report aggregated by day.</summary>
    [HttpGet("revenue")]
    [ProducesResponseType(typeof(PlatformRevenueReportDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Revenue(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct)
    {
        var result = await platformService.GetRevenueReportAsync(startDate, endDate, ct);
        return Ok(result);
    }

    /// <summary>Export platform revenue as CSV.</summary>
    [HttpGet("revenue/export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RevenueExportCsv(
        [FromQuery] DateOnly startDate,
        [FromQuery] DateOnly endDate,
        CancellationToken ct)
    {
        var rows = await platformService.GetRevenueCsvRowsAsync(startDate, endDate, ct);

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(rows, ct);
        }

        return File(stream.ToArray(), "text/csv",
            $"platform-revenue-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
    }
}

[ApiController]
[Route("api/v1/super-admin/audit-log")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Audit Log")]
public class SuperAdminAuditLogController(
    IAuditLogService auditLogService) : ControllerBase
{
    /// <summary>Query the global (unscoped) audit log across all centers.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuditLogEntryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> QueryGlobal(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default)
    {
        var result = await auditLogService.GetGlobalAuditLogsAsync(
            new AuditLogQueryDto(startDate, endDate, userId, action, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Export global audit log as CSV.</summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] Guid? userId,
        [FromQuery] string? action,
        CancellationToken ct)
    {
        var result = await auditLogService.GetGlobalAuditLogsAsync(
            new AuditLogQueryDto(startDate, endDate, userId, action, 1, 10000), ct);

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(result.Items, ct);
        }

        return File(stream.ToArray(), "text/csv",
            $"audit-log-global-{DateTime.UtcNow:yyyyMMdd}.csv");
    }
}
