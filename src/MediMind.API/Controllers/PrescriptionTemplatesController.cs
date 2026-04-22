using MediMind.Application.Features.Prescriptions;
using MediMind.Application.Features.PrescriptionTemplates;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Prescription templates — create, manage, and apply reusable prescription drafts.
/// </summary>
[ApiController]
[Authorize(Policy = "DoctorOnly")]
[Route("api/v1/prescription-templates")]
public class PrescriptionTemplatesController(
    IPrescriptionTemplateService templateService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create a new prescription template.</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpPost]
    [ProducesResponseType(typeof(PrescriptionTemplateResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreatePrescriptionTemplateDto dto, CancellationToken ct)
    {
        var created = await templateService.CreateAsync(currentUser.UserId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>List all templates for the authenticated doctor, sorted by usage count.</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<PrescriptionTemplateResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var templates = await templateService.ListAsync(currentUser.UserId, ct);
        return Ok(templates);
    }

    /// <summary>Get a single template by ID.</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(PrescriptionTemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var template = await templateService.GetByIdAsync(id, currentUser.UserId, ct);
        return template is null ? NotFound() : Ok(template);
    }

    /// <summary>Update a template.</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(PrescriptionTemplateResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdatePrescriptionTemplateDto dto, CancellationToken ct)
    {
        var updated = await templateService.UpdateAsync(id, currentUser.UserId, dto, ct);
        return Ok(updated);
    }

    /// <summary>Delete a template.</summary>
    [Tags("Doctor — Prescriptions")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await templateService.DeleteAsync(id, currentUser.UserId, ct);
        return NoContent();
    }
}
