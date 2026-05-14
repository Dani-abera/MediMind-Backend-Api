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

    /// <summary>Search healthcare centers by city, specialization, name, or availability (FR-031).</summary>
    [Tags("Public")]
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<CenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] string? city, [FromQuery] string? specialization, [FromQuery] string? name, [FromQuery] DateOnly? availableOnDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await centerService.SearchAsync(new CenterSearchDto(city, specialization, name, page, pageSize, availableOnDate));
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

    /// <summary>Get admin-facing general settings for this center (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpGet("{id:guid}/config")]
    [RequireRole("Admin", "SuperAdmin")]
    [ProducesResponseType(typeof(AdminCenterConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAdminConfig(Guid id, CancellationToken ct)
    {
        var result = await centerService.GetAdminConfigAsync(id, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Update general center info (name, phone, services, etc.) (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpPut("{id:guid}/general")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminCenterConfigDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateGeneralConfig(Guid id, [FromBody] AdminCenterConfigUpdateDto dto, CancellationToken ct)
    {
        var result = await centerService.UpdateAdminConfigAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Get booking rules for this center (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpGet("{id:guid}/booking-rules")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminBookingRulesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetBookingRules(Guid id, CancellationToken ct)
    {
        var result = await centerService.GetBookingRulesAsync(id, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Update booking rules (slot duration, advance booking, cancellation policy) (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpPut("{id:guid}/booking-rules")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminBookingRulesDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateBookingRules(Guid id, [FromBody] AdminBookingRulesDto dto, CancellationToken ct)
    {
        var result = await centerService.UpdateBookingRulesAsync(id, dto, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Get working hours for this center (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpGet("{id:guid}/working-hours")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminWorkingHoursDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetWorkingHours(Guid id, CancellationToken ct)
    {
        var result = await centerService.GetWorkingHoursAsync(id, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Update working hours for this center (FR-030).</summary>
    [Tags("Admin — Center Settings")]
    [HttpPut("{id:guid}/working-hours")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminWorkingHoursDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateWorkingHours(Guid id, [FromBody] AdminWorkingHoursDto dto, CancellationToken ct)
    {
        var result = await centerService.UpdateWorkingHoursAsync(id, dto, currentUser.UserId);
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

    /// <summary>Update a doctor's consultation fee at this healthcare center (FR-025).</summary>
    [Tags("Admin — Doctors")]
    [HttpPut("{id:guid}/doctors/{doctorId:guid}/fee")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(DoctorCenterRelationDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateConsultationFee(Guid id, Guid doctorId, [FromBody] UpdateConsultationFeeDto dto, CancellationToken ct)
    {
        var result = await centerService.UpdateConsultationFeeAsync(id, doctorId, dto, currentUser.UserId, ct);
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

    /// <summary>Export all analytics metrics as CSV (FR-041).</summary>
    [Tags("Admin — Analytics")]
    [HttpGet("{id:guid}/analytics/export/csv")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportCsv(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        try
        {
            var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);

            await using var stream = new MemoryStream();
            await using var writer = new StreamWriter(stream, Encoding.UTF8, leaveOpen: true);

            await writer.WriteLineAsync("Patient Volume Trends");
            await writer.WriteLineAsync("Date,Count");
            foreach (var p in dashboard.PatientVolumeTrends)
                await writer.WriteLineAsync($"{p.Date:yyyy-MM-dd},{p.Count}");

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("No-Show Rates by Doctor");
            await writer.WriteLineAsync("DoctorId,DoctorName,NoShowRate,Total");
            foreach (var n in dashboard.NoShowRateByDoctor)
                await writer.WriteLineAsync($"{n.DoctorId},{EscapeCsv(n.DoctorName)},{n.NoShowRate},{n.Total}");

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("Doctor Utilization");
            await writer.WriteLineAsync("DoctorId,DoctorName,UtilizationPercent,SlotsUsed,TotalSlots");
            foreach (var u in dashboard.DoctorUtilization)
                await writer.WriteLineAsync($"{u.DoctorId},{EscapeCsv(u.DoctorName)},{u.UtilizationPercent},{u.SlotsUsed},{u.TotalSlots}");

            await writer.WriteLineAsync();
            await writer.WriteLineAsync("Revenue");
            await writer.WriteLineAsync($"TotalRevenue,{dashboard.RevenueMetrics.TotalRevenue}");
            await writer.WriteLineAsync($"AveragePerAppointment,{dashboard.RevenueMetrics.AveragePerAppointment}");
            await writer.WriteLineAsync("DoctorId,Revenue");
            foreach (var r in dashboard.RevenueMetrics.ByDoctor)
                await writer.WriteLineAsync($"{r.DoctorId},{r.Revenue}");

            await writer.FlushAsync();
            return File(stream.ToArray(), "text/csv", $"analytics-{id}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.csv");
        }
        catch (Exception)
        {
            return Problem(statusCode: 500, title: "Export Failed",
                detail: "Export failed. Try again or contact support");
        }
    }

    private static string EscapeCsv(string value) =>
        value.Contains(',') || value.Contains('"') ? $"\"{value.Replace("\"", "\"\"")}\"" : value;

    /// <summary>Export full analytics report as PDF (FR-041).</summary>
    [Tags("Admin — Analytics")]
    [HttpGet("{id:guid}/analytics/export/pdf")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPdf(Guid id, [FromQuery] DateOnly startDate, [FromQuery] DateOnly endDate)
    {
        try
        {
            var dashboard = await analyticsService.GetDashboardAnalyticsAsync(id, currentUser.UserId, startDate, endDate);

            QuestPDF.Settings.License = LicenseType.Community;
            var bytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Content().Column(column =>
                    {
                        column.Spacing(10);

                        column.Item().Text($"Analytics Report: {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}").FontSize(16).Bold();

                        // Summary
                        column.Item().Text("Summary").FontSize(13).Bold();
                        column.Item().Text($"Total Appointments: {dashboard.Summary.TotalAppointments}");
                        column.Item().Text($"Completed: {dashboard.Summary.CompletedCount}");
                        column.Item().Text($"Cancelled: {dashboard.Summary.CancelledCount}");
                        column.Item().Text($"No-Show: {dashboard.Summary.NoShowCount}");
                        column.Item().Text($"Unique Patients: {dashboard.Summary.NewPatientsCount}");
                        column.Item().Text($"Average Wait Time: {dashboard.AverageWaitTimeMinutes?.ToString("F1") ?? "N/A"} min");

                        // Revenue
                        column.Item().Text("Revenue").FontSize(13).Bold();
                        column.Item().Text($"Total Revenue: {dashboard.RevenueMetrics.TotalRevenue:C}");
                        column.Item().Text($"Average Per Appointment: {dashboard.RevenueMetrics.AveragePerAppointment:C}");
                        if (dashboard.RevenueMetrics.ByDoctor.Count > 0)
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); });
                                table.Header(h => { h.Cell().Text("Doctor ID"); h.Cell().Text("Revenue"); });
                                foreach (var r in dashboard.RevenueMetrics.ByDoctor)
                                {
                                    table.Cell().Text(r.DoctorId.ToString());
                                    table.Cell().Text(r.Revenue.ToString("C"));
                                }
                            });
                        }

                        // No-Show Rates
                        if (dashboard.NoShowRateByDoctor.Count > 0)
                        {
                            column.Item().Text("No-Show Rates by Doctor").FontSize(13).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(1); });
                                table.Header(h => { h.Cell().Text("Doctor"); h.Cell().Text("No-Show Rate (%)"); h.Cell().Text("Total"); });
                                foreach (var n in dashboard.NoShowRateByDoctor)
                                {
                                    table.Cell().Text(n.DoctorName);
                                    table.Cell().Text(n.NoShowRate.ToString("F2"));
                                    table.Cell().Text(n.Total.ToString());
                                }
                            });
                        }

                        // Doctor Utilization
                        if (dashboard.DoctorUtilization.Count > 0)
                        {
                            column.Item().Text("Doctor Utilization").FontSize(13).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(2); c.RelativeColumn(2); });
                                table.Header(h => { h.Cell().Text("Doctor"); h.Cell().Text("Utilization (%)"); h.Cell().Text("Slots Used/Total"); });
                                foreach (var u in dashboard.DoctorUtilization)
                                {
                                    table.Cell().Text(u.DoctorName);
                                    table.Cell().Text(u.UtilizationPercent.ToString("F2"));
                                    table.Cell().Text($"{u.SlotsUsed}/{u.TotalSlots}");
                                }
                            });
                        }

                        // Patient Volume Trends
                        if (dashboard.PatientVolumeTrends.Count > 0)
                        {
                            column.Item().Text("Patient Volume Trends").FontSize(13).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(3); c.RelativeColumn(1); });
                                table.Header(h => { h.Cell().Text("Date"); h.Cell().Text("Count"); });
                                foreach (var v in dashboard.PatientVolumeTrends)
                                {
                                    table.Cell().Text(v.Date.ToString("yyyy-MM-dd"));
                                    table.Cell().Text(v.Count.ToString());
                                }
                            });
                        }

                        // Peak Hours
                        if (dashboard.PeakHoursHeatmap.Count > 0)
                        {
                            column.Item().Text("Peak Hours").FontSize(13).Bold();
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c => { c.RelativeColumn(2); c.RelativeColumn(2); c.RelativeColumn(1); });
                                table.Header(h => { h.Cell().Text("Day of Week"); h.Cell().Text("Hour"); h.Cell().Text("Count"); });
                                foreach (var ph in dashboard.PeakHoursHeatmap)
                                {
                                    table.Cell().Text(((DayOfWeek)ph.DayOfWeek).ToString());
                                    table.Cell().Text($"{ph.Hour:00}:00");
                                    table.Cell().Text(ph.Count.ToString());
                                }
                            });
                        }
                    });
                });
            }).GeneratePdf();

            return File(bytes, "application/pdf", $"analytics-{id}-{startDate:yyyyMMdd}-{endDate:yyyyMMdd}.pdf");
        }
        catch (Exception)
        {
            return Problem(statusCode: 500, title: "Export Failed",
                detail: "Export failed. Try again or contact support");
        }
    }
}
