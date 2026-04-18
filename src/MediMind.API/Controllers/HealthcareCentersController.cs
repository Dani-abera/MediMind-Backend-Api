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
/// Healthcare center management and analytics endpoints.
/// </summary>
[ApiController]
[Route("api/v1/healthcare-centers")]
[Tags("Healthcare centers")]
public class HealthcareCentersController(
    IHealthcareCenterService centerService,
    IAnalyticsService analyticsService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "AdminOnly")]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Register([FromBody] RegisterCenterDto dto)
    {
        var result = await centerService.RegisterCenterAsync(dto, currentUser.UserId);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<CenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? city, [FromQuery] string? specialization, [FromQuery] string? name, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await centerService.SearchAsync(new CenterSearchDto(city, specialization, name, page, pageSize));
        return Ok(result);
    }

    [HttpGet("nearby")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(IEnumerable<CenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Nearby([FromQuery] double latitude, [FromQuery] double longitude, [FromQuery] double radiusKm = 10)
    {
        var result = await centerService.SearchNearbyAsync(latitude, longitude, radiusKm);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await centerService.GetByIdAsync(id);
        return Ok(result);
    }

    [HttpPut("{id:guid}/config")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(CenterResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConfig(Guid id, [FromBody] CenterConfigurationDto dto)
    {
        var result = await centerService.UpdateConfigurationAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    [HttpPost("{id:guid}/doctors")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(DoctorCenterRelationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> AddDoctor(Guid id, [FromBody] AddDoctorDto dto)
    {
        var result = await centerService.AddDoctorToCenterAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/doctors/{doctorId:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDoctor(Guid id, Guid doctorId)
    {
        var removed = await centerService.RemoveDoctorFromCenterAsync(id, doctorId, currentUser.UserId);
        if (!removed)
            return NotFound(new { error = "Doctor relation not found" });
        return NoContent();
    }

    [HttpGet("{id:guid}/doctors")]
    [Authorize]
    [ProducesResponseType(typeof(IEnumerable<DoctorResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(Guid id)
    {
        var doctors = await centerService.GetDoctorsAsync(id);
        return Ok(doctors);
    }

    [HttpGet("{id:guid}/analytics")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AnalyticsDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Analytics(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);
        return Ok(dashboard);
    }

    [HttpGet("{id:guid}/analytics/export/csv")]
    [RequireRole("Admin")]
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

    [HttpGet("{id:guid}/analytics/export/pdf")]
    [RequireRole("Admin")]
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
