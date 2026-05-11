using CsvHelper;
using MediMind.API.Attributes;
using MediMind.Application.Features.CenterManagement;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;
using System.Text;

namespace MediMind.API.Controllers;

/// <summary>
/// Healthcare center registration, configuration, doctor relations, and analytics.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/healthcare-centers")]
public class HealthcareCentersController(
    IHealthcareCenterService centerService,
    IAnalyticsService analyticsService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Register a new healthcare center for the authenticated admin (FR-030).</summary>
    [Tags("Admin — Center")]
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterCenterDto dto, CancellationToken ct)
    {
        var result = await centerService.RegisterCenterAsync(dto, currentUser.UserId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Search healthcare centers by city, specialization, or name (FR-031).</summary>
    [Tags("Public")]
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<CenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? city, [FromQuery] string? specialization, [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await centerService.SearchAsync(new CenterSearchDto(city, specialization, name, page, pageSize));
        return Ok(result);
    }

    /// <summary>Find healthcare centers near a geographic coordinate (FR-031).</summary>
    [Tags("Public")]
    [HttpGet("nearby")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Nearby([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radiusKm = 10, CancellationToken ct = default)
    {
        var result = await centerService.SearchNearbyAsync(latitude, longitude, radiusKm);
        return Ok(result);
    }

    /// <summary>Get the healthcare center registered by the authenticated admin.</summary>
    /// <remarks>Returns the center linked to the admin's JWT tenant claim. 404 if the admin has not registered a center yet.</remarks>
    [Tags("Admin — Center")]
    [HttpGet("mine")]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyCenter(CancellationToken ct)
    {
        if (currentUser.TenantId is not { } centerId)
            return NotFound(new { error = "No healthcare center is linked to your account yet. Please register one first." });
        var result = await centerService.GetByIdAsync(centerId);
        return Ok(result);
    }

    /// <summary>Get a healthcare center's full details by ID (FR-031).</summary>
    [Tags("Public")]
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await centerService.GetByIdAsync(id);
        return Ok(result);
    }

    /// <summary>Update center configuration (capacity, queue settings, etc.) (FR-032).</summary>
    [Tags("Admin — Center")]
    [HttpPut("{id:guid}/config")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfig(Guid id, [FromBody] CenterConfigurationDto dto, CancellationToken ct)
    {
        var result = await centerService.UpdateConfigurationAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Add an existing doctor to this healthcare center (FR-033).</summary>
    [Tags("Admin — Doctors")]
    [HttpPost("{id:guid}/doctors")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(DoctorCenterRelationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDoctor(Guid id, [FromBody] AddDoctorDto dto, CancellationToken ct)
    {
        var result = await centerService.AddDoctorToCenterAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Remove a doctor from this healthcare center (FR-033).</summary>
    [Tags("Admin — Doctors")]
    [HttpDelete("{id:guid}/doctors/{doctorId:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveDoctor(Guid id, Guid doctorId, CancellationToken ct)
    {
        var removed = await centerService.RemoveDoctorFromCenterAsync(id, doctorId, currentUser.UserId);
        if (!removed)
            return NotFound(new { error = "Doctor relation not found" });
        return NoContent();
    }

    /// <summary>List all active doctors affiliated with a healthcare center (FR-031).</summary>
    [Tags("Public")]
    [HttpGet("{id:guid}/doctors")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<DoctorResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(Guid id, CancellationToken ct)
    {
        var doctors = await centerService.GetDoctorsAsync(id);
        return Ok(doctors);
    }

    /// <summary>Get the analytics dashboard for a healthcare center (FR-040).</summary>
    [Tags("Admin — Analytics")]
    [HttpGet("{id:guid}/analytics")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AnalyticsDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analytics(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate, CancellationToken ct)
    {
        var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);
        return Ok(dashboard);
    }

    /// <summary>Export patient volume trend data as CSV (FR-041).</summary>
    [Tags("Admin — Analytics")]
    [HttpGet("{id:guid}/analytics/export/csv")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);

        await using var stream = new MemoryStream();
        await using (var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true))
        await using (var csv = new CsvWriter(writer, CultureInfo.InvariantCulture))
        {
            await csv.WriteRecordsAsync(dashboard.PatientVolumeTrends);
        }

        return File(stream.ToArray(), "text/csv", $"patient-volume-{id}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
    }

    /// <summary>Export analytics summary as a PDF report (FR-041).</summary>
    [Tags("Admin — Analytics")]
    [HttpGet("{id:guid}/analytics/export/pdf")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);

        QuestPDF.Settings.License = LicenseType.Community;
        var bytes = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(24);
                page.Content().Column(column =>
                {
                    column.Item().Text($"Analytics Report: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}").Bold();
                    column.Item().Text($"Total Appointments: {dashboard.Summary.TotalAppointments}");
                    column.Item().Text($"Completed: {dashboard.Summary.CompletedCount}");
                    column.Item().Text($"No Show: {dashboard.Summary.NoShowCount}");
                    column.Item().Text($"Average Wait: {dashboard.AverageWaitTimeMinutes?.ToString("F1") ?? "N/A"} minutes");
                });
            });
        }).GeneratePdf();

        return File(bytes, "application/pdf", $"analytics-{id}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.pdf");
    }
}
