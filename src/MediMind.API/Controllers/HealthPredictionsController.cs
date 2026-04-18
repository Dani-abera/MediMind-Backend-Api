using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.HealthRecords;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// AI health prediction APIs powered by external ML service.
/// </summary>
[ApiController]
[Authorize]
[Tags("Health predictions")]
[Route("api/v1/health-predictions")]
public class HealthPredictionsController(
    IHealthPredictionService healthPredictionService,
    IHealthRecordService healthRecordService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Requests a new AI prediction for the authenticated patient.
    /// </summary>
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
        catch (ValidationException ex)
        {
            return UnprocessableEntity(new { error = ex.Message });
        }
        catch (ServiceUnavailableException ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { error = ex.Message });
        }
    }

    /// <summary>
    /// Gets paginated prediction history for authenticated patient.
    /// </summary>
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

    /// <summary>
    /// Gets latest prediction by role-aware access.
    /// </summary>
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

    /// <summary>
    /// Gets a specific prediction by identifier with role-aware access.
    /// </summary>
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

    /// <summary>
    /// Gets prediction readiness status for authenticated patient.
    /// </summary>
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
