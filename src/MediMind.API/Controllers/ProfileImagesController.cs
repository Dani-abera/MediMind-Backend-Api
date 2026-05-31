using MediatR;
using MediMind.Application.Features.ProfileImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>Profile and logo image management for users and healthcare centers.</summary>
[ApiController]
[Route("api/v1/profile-images")]
public class ProfileImagesController(IMediator mediator) : ControllerBase
{
    /// <summary>Get the signed-in user's profile image URL (FR-003).</summary>
    [Tags("Patient — Profile", "Doctor — Profile")]
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await mediator.Send(new GetMyProfileImageQuery(), ct);
        return Ok(result);
    }

    /// <summary>Upload or replace the signed-in user's profile image (JPEG, PNG, WebP, GIF; max 5 MB) (FR-003).</summary>
    [Tags("Patient — Profile", "Doctor — Profile")]
    [HttpPut("me")]
    [Authorize]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [RequestSizeLimit(ProfileImageRules.MaxBytes)]
    public async Task<IActionResult> UploadMine(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        await using var stream = file.OpenReadStream();
        var command = new UploadMyProfileImageCommand(
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length);
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Remove the signed-in user's profile image (FR-003).</summary>
    [Tags("Patient — Profile", "Doctor — Profile")]
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMine(CancellationToken ct)
    {
        await mediator.Send(new DeleteMyProfileImageCommand(), ct);
        return NoContent();
    }
}
