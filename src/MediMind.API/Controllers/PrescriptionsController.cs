using MediMind.Application.Features.Prescriptions;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>Digital prescriptions — PDF, QR verification, dispensing.</summary>
[Tags("Prescriptions v1")]
[Authorize]
[Route("api/v1/prescriptions")]
[ApiController]
public class PrescriptionsController(
    IPrescriptionService prescriptionService,
    ICurrentUser currentUser) : ControllerBase
{
    [HttpPost]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionDto dto, CancellationToken ct)
    {
        var result = await prescriptionService.CreatePrescriptionAsync(dto, currentUser.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.PrescriptionId }, result);
    }

    [HttpGet]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(IReadOnlyList<PrescriptionResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var items = await prescriptionService.ListForRequesterAsync(
            currentUser.UserId,
            currentUser.UserType,
            page,
            pageSize,
            ct);
        return Ok(items);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        if (currentUser.UserType is not ("Patient" or "Doctor" or "Admin"))
            return Forbid();

        var tenant = currentUser.UserType == "Admin" ? currentUser.TenantId : null;

        var result = await prescriptionService.GetDetailsAsync(
            id,
            currentUser.UserId,
            currentUser.UserType,
            tenant,
            ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    public async Task<IActionResult> DownloadPdf(Guid id, CancellationToken ct)
    {
        if (currentUser.UserType is not ("Patient" or "Doctor" or "Admin"))
            return Forbid();

        var tenant = currentUser.UserType == "Admin" ? currentUser.TenantId : null;

        var (pdf, fileName) = await prescriptionService.GetPrescriptionPdfAsync(
            id,
            currentUser.UserId,
            currentUser.UserType,
            tenant,
            ct);

        Response.Headers.ContentDisposition = $"attachment; filename=\"{fileName}\"";
        return File(pdf, "application/pdf", fileName);
    }

    [HttpGet("appointment/{appointmentId:guid}")]
    [Authorize(Policy = "PatientOrDoctor")]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByAppointment(Guid appointmentId, CancellationToken ct)
    {
        var result = await prescriptionService.GetByAppointmentAsync(
            appointmentId,
            currentUser.UserId,
            currentUser.UserType,
            ct);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpPost("{id:guid}/mark-dispensed")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkDispensed(Guid id, CancellationToken ct)
    {
        await prescriptionService.MarkDispensedAsync(
            id,
            currentUser.UserId,
            currentUser.UserType,
            currentUser.TenantId,
            ct);
        return NoContent();
    }

    [HttpGet("verify/{prescriptionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PrescriptionVerificationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(Guid prescriptionId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await prescriptionService.VerifyPrescriptionAsync(prescriptionId, token, ct);
        return Ok(result);
    }
}
