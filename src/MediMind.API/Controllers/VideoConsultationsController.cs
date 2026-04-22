using MediMind.Application.Features.VideoConsultations;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Video consultation sessions — WebRTC signaling, chat, and quality reporting (FR-110–FR-116).
/// </summary>
[ApiController]
[Route("api/v1/video-consultations")]
[Authorize]
[Tags("Doctor — Video Consultations")]
public sealed class VideoConsultationsController(IVideoConsultationService service, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Initiate a new video consultation session for an appointment (FR-110).</summary>
    [HttpPost("initiate")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<ConsultationSessionDto>> Initiate([FromBody] InitiateConsultationRequest request, CancellationToken ct)
    {
        var result = await service.InitiateConsultationAsync(request.AppointmentId, currentUser.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.ConsultationId }, result);
    }

    /// <summary>Join an existing video consultation session (FR-111).</summary>
    [HttpPost("{id:guid}/join")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationJoinDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsultationJoinDto>> Join(Guid id, CancellationToken ct)
    {
        var result = await service.JoinConsultationAsync(id, currentUser.UserId, currentUser.UserType, null, ct);
        return Ok(result);
    }

    /// <summary>End an active video consultation session (FR-112).</summary>
    [HttpPost("{id:guid}/end")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> End(Guid id, CancellationToken ct)
    {
        var endedByDoctor = string.Equals(currentUser.UserType, "Doctor", StringComparison.OrdinalIgnoreCase);
        await service.EndConsultationAsync(id, currentUser.UserId, endedByDoctor, ct);
        return NoContent();
    }

    /// <summary>Get session details for a video consultation (FR-113).</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConsultationSessionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    /// <summary>Get the in-session chat history for a video consultation (FR-114).</summary>
    [HttpGet("{id:guid}/chat")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(IReadOnlyList<ChatMessageDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> Chat(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await service.GetChatHistoryAsync(id, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Get the video consultation linked to an appointment (FR-113).</summary>
    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(ConsultationSessionDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConsultationSessionDto>> ByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await service.GetByAppointmentAsync(appointmentId, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    /// <summary>Submit a connection quality report (bandwidth, packet loss, frame rate) (FR-116).</summary>
    [HttpPost("{id:guid}/quality-report")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> QualityReport(Guid id, [FromBody] QualityReportRequest request, CancellationToken ct)
    {
        await service.ReportQualityAsync(id, currentUser.UserId, request.Bandwidth, request.PacketsLost, request.FrameRate, ct);
        return Accepted();
    }
}

public sealed record InitiateConsultationRequest(Guid AppointmentId);
public sealed record QualityReportRequest(int Bandwidth, int PacketsLost, int FrameRate);
