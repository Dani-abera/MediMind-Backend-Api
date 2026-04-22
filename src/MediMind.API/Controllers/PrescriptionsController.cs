using MediMind.Application.Features.Prescriptions;
using MediMind.Application.Features.PrescriptionTemplates;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>Digital prescriptions — creation, PDF download, QR verification, and dispensing (FR-100–FR-106).</summary>
[Authorize]
[Route("api/v1/prescriptions")]
[ApiController]
public class PrescriptionsController(
    IPrescriptionService prescriptionService,
    IPrescriptionTemplateService templateService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create a new digital prescription for a patient (FR-100).</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpPost]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionDto dto, CancellationToken ct)
    {
        var result = await prescriptionService.CreatePrescriptionAsync(dto, currentUser.UserId, ct);
        return CreatedAtAction(nameof(GetById), new { id = result.PrescriptionId }, result);
    }

    /// <summary>List prescriptions for the current user. Patients see their own; doctors see prescriptions they issued (FR-101).</summary>
    [Tags("Patient — Prescriptions", "Doctor — Prescriptions")]
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

    /// <summary>Get full prescription details by ID (FR-102).</summary>
    [Tags("Patient — Prescriptions", "Doctor — Prescriptions")]
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

    /// <summary>Download a prescription as a signed PDF file (FR-103).</summary>
    [Tags("Patient — Prescriptions", "Doctor — Prescriptions")]
    [HttpGet("{id:guid}/pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
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

    /// <summary>Get the prescription attached to a specific appointment (FR-104).</summary>
    [Tags("Patient — Prescriptions", "Doctor — Prescriptions")]
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

    /// <summary>Mark a prescription as dispensed by the doctor or admin (FR-105).</summary>
    [Tags("Doctor — Prescriptions")]
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

    /// <summary>Create a prescription from a saved template, with optional field overrides (FR-100).</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpPost("from-template")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(PrescriptionResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> CreateFromTemplate([FromBody] CreatePrescriptionFromTemplateDto dto, CancellationToken ct)
    {
        var result = await templateService.CreateFromTemplateAsync(currentUser.UserId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Revoke a prescription (FR-020 edge case). Roles: Doctor (issuer) or Admin (same center).</summary>
    [Tags("Doctor — Prescriptions", "Admin — Prescriptions")]
    [HttpPost("{id:guid}/revoke")]
    [Authorize(Policy = "DoctorOrAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Revoke(Guid id, [FromBody] RevokePrescriptionApiRequest request, CancellationToken ct)
    {
        await prescriptionService.RevokePrescriptionAsync(
            id,
            request.Reason,
            currentUser.UserId,
            currentUser.UserType,
            currentUser.TenantId,
            ct);
        return NoContent();
    }

    /// <summary>Publicly verify a prescription using its QR token (FR-106).</summary>
    [Tags("Public")]
    [HttpGet("verify/{prescriptionId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PrescriptionVerificationDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Verify(Guid prescriptionId, [FromQuery] string token, CancellationToken ct)
    {
        var result = await prescriptionService.VerifyPrescriptionAsync(prescriptionId, token, ct);
        return Ok(result);
    }
}

public record RevokePrescriptionApiRequest(string Reason);
