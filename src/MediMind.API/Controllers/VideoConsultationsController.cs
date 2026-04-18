using MediMind.Application.Features.VideoConsultations;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

[ApiController]
[Route("api/v1/video-consultations")]
[Authorize]
[Tags("Video Consultations")]
public sealed class VideoConsultationsController(IVideoConsultationService service, ICurrentUser currentUser) : ControllerBase
{
    [HttpPost("initiate")]
    [Authorize(Policy = "DoctorOnly")]
    public async Task<ActionResult<ConsultationSessionDto>> Initiate([FromBody] InitiateConsultationRequest request, CancellationToken ct)
    {
        var result = await service.InitiateConsultationAsync(request.AppointmentId, currentUser.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.ConsultationId }, result);
    }

    [HttpPost("{id:guid}/join")]
    [Authorize(Policy = "PatientOrDoctor")]
    public async Task<ActionResult<ConsultationJoinDto>> Join(Guid id, CancellationToken ct)
    {
        var result = await service.JoinConsultationAsync(id, currentUser.UserId, currentUser.UserType, null, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/end")]
    [Authorize(Policy = "DoctorOrAdmin")]
    public async Task<IActionResult> End(Guid id, CancellationToken ct)
    {
        var endedByDoctor = string.Equals(currentUser.UserType, "Doctor", StringComparison.OrdinalIgnoreCase);
        await service.EndConsultationAsync(id, currentUser.UserId, endedByDoctor, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    public async Task<ActionResult<ConsultationSessionDto>> GetById(Guid id, CancellationToken ct)
    {
        var result = await service.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/chat")]
    [Authorize(Policy = "PatientOrDoctor")]
    public async Task<ActionResult<IReadOnlyList<ChatMessageDto>>> Chat(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 50, CancellationToken ct = default)
    {
        var result = await service.GetChatHistoryAsync(id, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    public async Task<ActionResult<ConsultationSessionDto>> ByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await service.GetByAppointmentAsync(appointmentId, currentUser.UserId, currentUser.UserType, ct);
        return Ok(result);
    }

    [HttpPost("{id:guid}/quality-report")]
    [Authorize(Policy = "PatientOrDoctor")]
    public async Task<IActionResult> QualityReport(Guid id, [FromBody] QualityReportRequest request, CancellationToken ct)
    {
        await service.ReportQualityAsync(id, currentUser.UserId, request.Bandwidth, request.PacketsLost, request.FrameRate, ct);
        return Accepted();
    }
}

public sealed record InitiateConsultationRequest(Guid AppointmentId);
public sealed record QualityReportRequest(int Bandwidth, int PacketsLost, int FrameRate);
