using MediMind.API.Attributes;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.HealthRecords;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// AI-powered health predictions powered by the external ML microservice (FR-060–FR-064).
/// </summary>
[ApiController]
[Authorize]
[Tags("Patient — Predictions")]
[Route("api/v1/health-predictions")]
public class HealthPredictionsController(
    IHealthPredictionService healthPredictionService,
    IHealthRecordService healthRecordService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Request a new AI health-risk prediction for the authenticated patient. Requires ≥ 7 health records (FR-060).</summary>
    [HttpPost("request")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(HealthPredictionResponseDto), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RequestPrediction()
    {
        try
        {
            var response = await healthPredictionService.RequestPredictionAsync(currentUser.UserId);
            return Accepted(response);
        }
        catch (ServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    /// <summary>Get paginated AI prediction history for the authenticated patient (FR-061).</summary>
    [HttpGet]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(IEnumerable<HealthPredictionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var safePage = Math.Max(page, 1);
        var safePageSize = Math.Clamp(pageSize, 1, 100);
        var result = await healthPredictionService.GetByPatientIdAsync(currentUser.UserId, safePage, safePageSize);
        return Ok(result);
    }

    /// <summary>Get the most recent prediction. Patients see their own; doctors provide patientId query param (FR-062).</summary>
    [HttpGet("latest")]
    [RequireRole("Patient", "Doctor")]
    [ProducesResponseType(typeof(HealthPredictionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLatest([FromQuery] Guid? patientId = null)
    {
        try
        {
            var targetPatientId = await ResolvePatientContextAsync(patientId);
            var latest = await healthPredictionService.GetLatestAsync(targetPatientId);
            if (latest is null)
                return NotFound(new { error = "Prediction not found" });

            return Ok(latest);
        }
        catch (UnauthorizedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied" });
        }
    }

    /// <summary>Get a specific prediction by ID with role-aware access (FR-063).</summary>
    [HttpGet("{id:guid}")]
    [RequireRole("Patient", "Doctor")]
    [ProducesResponseType(typeof(HealthPredictionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, [FromQuery] Guid? patientId = null)
    {
        try
        {
            var targetPatientId = await ResolvePatientContextAsync(patientId);
            var prediction = await healthPredictionService.GetByIdAsync(id, targetPatientId);
            if (prediction is null)
                return NotFound(new { error = "Prediction not found" });

            return Ok(prediction);
        }
        catch (UnauthorizedException)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { error = "Access denied" });
        }
    }

    /// <summary>Get prediction readiness status — record count, eligibility, and last prediction date (FR-064).</summary>
    [HttpGet("status")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(PredictionRequestStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus()
    {
        var status = await healthPredictionService.GetStatusAsync(currentUser.UserId);
        return Ok(status);
    }

    private async Task<Guid> ResolvePatientContextAsync(Guid? patientId)
    {
        if (currentUser.UserType == "Patient")
            return currentUser.UserId;

        if (!patientId.HasValue || patientId.Value == Guid.Empty)
            throw new UnauthorizedException();

        var latestHealthRecord = await healthRecordService.GetLatestAsync(patientId.Value);
        if (latestHealthRecord is null)
            throw new UnauthorizedException();

        var accessCheck = await healthRecordService.GetByIdAsync(
            latestHealthRecord.RecordId,
            currentUser.UserId,
            currentUser.UserType,
            currentUser.TenantId);

        if (accessCheck is null)
            throw new UnauthorizedException();

        return patientId.Value;
    }
}
