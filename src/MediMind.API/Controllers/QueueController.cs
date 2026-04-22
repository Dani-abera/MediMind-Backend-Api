using MediMind.API.Attributes;
using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

public record CallNextRequest(Guid CenterId);
public record EmergencyInsertRequest(Guid AppointmentId, Guid CenterId);

/// <summary>
/// Queue management with SignalR-backed real-time updates (FR-025–FR-029).
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/queue")]
public class QueueController(IQueueService queueService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Get the queue position and wait estimate for a patient's appointment (FR-025).</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("status/{appointmentId:guid}")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(PatientQueueStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(Guid appointmentId)
    {
        var result = await queueService.GetQueueStatusAsync(appointmentId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Get the full queue dashboard for a center and date (FR-026).</summary>
    [Tags("Admin — Queue")]
    [HttpGet("center/{centerId:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminQueueDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenterDashboard(Guid centerId, [FromQuery] DateOnly? date = null)
    {
        var result = await queueService.GetCenterDashboardAsync(centerId, date ?? DateOnly.FromDateTime(DateTime.UtcNow), currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Call the next waiting patient to the consultation room (FR-027).</summary>
    [Tags("Admin — Queue")]
    [HttpPost("call-next")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CallNext([FromBody] CallNextRequest request)
    {
        var result = await queueService.CallNextPatientAsync(request.CenterId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Mark a patient as arrived at the facility (FR-028).</summary>
    [Tags("Admin — Queue")]
    [HttpPost("{queueId:guid}/arrived")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Arrived(Guid queueId)
    {
        var result = await queueService.MarkPatientArrivedAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Mark a consultation as complete and advance the queue (FR-028).</summary>
    [Tags("Admin — Queue")]
    [HttpPost("{queueId:guid}/complete")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid queueId)
    {
        var result = await queueService.MarkConsultationCompleteAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Mark a patient as a no-show (FR-028).</summary>
    [Tags("Admin — Queue")]
    [HttpPost("{queueId:guid}/no-show")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> NoShow(Guid queueId)
    {
        var result = await queueService.MarkNoShowAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>Insert an emergency patient at the front of the queue (FR-029).</summary>
    [Tags("Admin — Queue")]
    [HttpPost("emergency")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Emergency([FromBody] EmergencyInsertRequest request)
    {
        if (currentUser.TenantId != request.CenterId)
            return Forbid();

        var result = await queueService.InsertEmergencyPatientAsync(request.AppointmentId, currentUser.UserId);
        return Ok(result);
    }

    // Queue regeneration moved to POST /api/v1/super-admin/centers/{id}/queue/regenerate
}
