using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.HealthRecords;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Patient vitals, health records, and trend analysis (FR-050–FR-056).
/// </summary>
[ApiController]
[Authorize]
[Tags("Patient — Health Records")]
[Route("api/v1/health-records")]
public class HealthRecordsController(
    IHealthRecordService healthRecordService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create a new health record for the authenticated patient (FR-050).</summary>
    [HttpPost]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Create([FromBody] CreateHealthRecordDto dto)
    {
        try
        {
            var created = await healthRecordService.CreateAsync(currentUser.UserId, dto);
            return CreatedAtAction(nameof(GetById), new { id = created.RecordId }, created);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(new { errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()) });
        }
        catch (DomainException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>List the authenticated patient's health records with pagination and date filters (FR-051).</summary>
    [HttpGet]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(PaginatedHealthRecordsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMy(
        [FromQuery] DateOnly? startDate,
        [FromQuery] DateOnly? endDate,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var boundedPageSize = Math.Clamp(pageSize, 1, 100);
        var result = await healthRecordService.GetByPatientAsync(currentUser.UserId, startDate, endDate, Math.Max(page, 1), boundedPageSize);
        return Ok(result);
    }

    /// <summary>Get a specific health record by ID. Patients see their own; doctors see records of their patients (FR-052).</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        try
        {
            var result = await healthRecordService.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, currentUser.TenantId);
            if (result is null)
                return NotFound(new { error = "Health record not found" });
            return Ok(result);
        }
        catch (UnauthorizedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied" });
        }
    }

    /// <summary>Update an existing health record for the authenticated patient (FR-053).</summary>
    [HttpPut("{id:guid}")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateHealthRecordDto dto, CancellationToken ct)
    {
        try
        {
            var updated = await healthRecordService.UpdateAsync(id, currentUser.UserId, dto);
            if (updated is null)
                return NotFound(new { error = "Health record not found" });
            return Ok(updated);
        }
        catch (ValidationException ex)
        {
            return UnprocessableEntity(new { errors = ex.Errors.GroupBy(e => e.PropertyName).ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray()) });
        }
    }

    /// <summary>Delete a health record for the authenticated patient (FR-054).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("Patient")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await healthRecordService.DeleteAsync(id, currentUser.UserId);
        if (!deleted)
            return NotFound(new { error = "Health record not found" });
        return NoContent();
    }

    /// <summary>Get vitals trend statistics for the authenticated patient over a rolling window (FR-055).</summary>
    [HttpGet("trends")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(HealthTrendDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrends([FromQuery] int days = 30, CancellationToken ct = default)
    {
        var trend = await healthRecordService.GetTrendAsync(currentUser.UserId, days);
        return Ok(trend);
    }

    /// <summary>Get the most recent health record. Patients see their own; doctors provide patientId query param (FR-056).</summary>
    [HttpGet("latest")]
    [RequireRole(["Patient", "Doctor"])]
    [ProducesResponseType(typeof(HealthRecordResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetLatest([FromQuery] Guid? patientId = null)
    {
        try
        {
            var targetPatientId = currentUser.UserType == "Patient" ? currentUser.UserId : patientId ?? Guid.Empty;
            if (targetPatientId == Guid.Empty)
                return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied" });

            if (currentUser.UserType == "Patient")
                healthRecordService.ValidatePatientOwnership(currentUser.UserId, targetPatientId);

            var latest = await healthRecordService.GetLatestAsync(targetPatientId);
            if (latest is null)
                return NotFound(new { error = "Health record not found" });

            if (currentUser.UserType == "Doctor")
            {
                var check = await healthRecordService.GetByIdAsync(latest.RecordId, currentUser.UserId, currentUser.UserType, currentUser.TenantId);
                if (check is null)
                    return NotFound(new { error = "Health record not found" });
            }

            return Ok(latest);
        }
        catch (UnauthorizedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied" });
        }
    }

    /// <summary>Get total record count and whether the patient has enough records (≥ 7) to request an AI prediction (FR-056).</summary>
    [HttpGet("count")]
    [RequireRole("Patient")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCount(CancellationToken ct)
    {
        var count = await healthRecordService.GetCountAsync(currentUser.UserId);
        return Ok(new { count, canRequestPrediction = count >= 7 });
    }
}
