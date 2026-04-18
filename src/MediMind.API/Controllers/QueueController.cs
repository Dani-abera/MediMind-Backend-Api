using MediMind.API.Attributes;
using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

public record CallNextRequest(Guid CenterId);
public record EmergencyInsertRequest(Guid AppointmentId, Guid CenterId);

/// <summary>
/// Queue management endpoints with SignalR-backed updates.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/queue")]
[Tags("Queue")]
public class QueueController(IQueueService queueService, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Gets patient queue status for appointment.
    /// </summary>
    [HttpGet("status/{appointmentId:guid}")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(PatientQueueStatusDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetStatus(Guid appointmentId)
    {
        var result = await queueService.GetQueueStatusAsync(appointmentId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Gets admin queue dashboard for center and date.
    /// </summary>
    [HttpGet("center/{centerId:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AdminQueueDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenterDashboard(Guid centerId, [FromQuery] DateOnly? date = null)
    {
        var result = await queueService.GetCenterDashboardAsync(centerId, date ?? DateOnly.FromDateTime(DateTime.UtcNow), currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Calls next waiting patient.
    /// </summary>
    [HttpPost("call-next")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> CallNext([FromBody] CallNextRequest request)
    {
        var result = await queueService.CallNextPatientAsync(request.CenterId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Marks patient arrived.
    /// </summary>
    [HttpPost("{queueId:guid}/arrived")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Arrived(Guid queueId)
    {
        var result = await queueService.MarkPatientArrivedAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Marks consultation complete.
    /// </summary>
    [HttpPost("{queueId:guid}/complete")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Complete(Guid queueId)
    {
        var result = await queueService.MarkConsultationCompleteAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Marks no-show.
    /// </summary>
    [HttpPost("{queueId:guid}/no-show")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(QueueItemDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> NoShow(Guid queueId)
    {
        var result = await queueService.MarkNoShowAsync(queueId, currentUser.UserId);
        return Ok(result);
    }

    /// <summary>
    /// Inserts emergency patient into first queue position.
    /// </summary>
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

    /// <summary>
    /// Manually triggers queue generation for center and date.
    /// </summary>
    [HttpGet("generate/{centerId:guid}")]
    [RequireRole("Admin", "SuperAdmin")]
    [ProducesResponseType(typeof(AdminQueueDashboardDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Generate(Guid centerId, [FromQuery] DateOnly? date = null)
    {
        var result = await queueService.GenerateQueueForCenterAsync(centerId, date ?? DateOnly.FromDateTime(DateTime.UtcNow), currentUser.UserId);
        return Ok(result);
    }
}
