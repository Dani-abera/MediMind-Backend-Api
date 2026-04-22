using MediMind.API.Attributes;
using MediMind.Application.Features.HealthRecordAttachments;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>File attachments (PDFs, images) on health records.</summary>
[ApiController]
[Authorize(Policy = "PatientOnly")]
[Tags("Patient — Health Records")]
[Route("api/v1/health-records/{id:guid}/attachments")]
public class HealthRecordAttachmentsController(
    IHealthRecordAttachmentService attachmentService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Upload a file attachment to a health record (PDF or image, max 10 MB).</summary>
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(HealthRecordAttachmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Upload(Guid id, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "No file provided." });

        await using var stream = file.OpenReadStream();
        var result = await attachmentService.UploadAsync(
            id, currentUser.UserId,
            stream, file.FileName,
            file.ContentType, file.Length, ct);

        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>List all attachments for a health record.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<HealthRecordAttachmentResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetAll(Guid id, CancellationToken ct) =>
        Ok(await attachmentService.GetByRecordAsync(id, currentUser.UserId, ct));

    /// <summary>Get a single attachment by ID.</summary>
    [HttpGet("{attachmentId:guid}")]
    [ProducesResponseType(typeof(HealthRecordAttachmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var result = await attachmentService.GetByIdAsync(id, attachmentId, currentUser.UserId, ct);
        if (result is null)
            return NotFound(new { error = "Attachment not found." });
        return Ok(result);
    }

    /// <summary>Delete an attachment from a health record.</summary>
    [HttpDelete("{attachmentId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, Guid attachmentId, CancellationToken ct)
    {
        await attachmentService.DeleteAsync(id, attachmentId, currentUser.UserId, ct);
        return NoContent();
    }
}
