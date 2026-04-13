using MediatR;
using MediMind.Application.Features.ProfileImages;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>Profile / logo images for patients, doctors, admins, and healthcare centers.</summary>
[Tags("Profile images")]
[Route("api/v1/profile-images")]
public class ProfileImagesController(IMediator mediator) : BaseController(mediator)
{
    /// <summary>Get the signed-in user's profile image URL.</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var result = await Mediator.Send(new GetMyProfileImageQuery(), ct);
        return Ok(result);
    }

    /// <summary>Upload or replace the signed-in user's profile image (JPEG, PNG, WebP, GIF; max 5 MB).</summary>
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
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Remove the signed-in user's profile image.</summary>
    [HttpDelete("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeleteMine(CancellationToken ct)
    {
        await Mediator.Send(new DeleteMyProfileImageCommand(), ct);
        return NoContent();
    }

    /// <summary>Get a healthcare center's logo / profile image URL (public).</summary>
    [HttpGet("healthcare-centers/{centerId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCenterImage(Guid centerId, CancellationToken ct)
    {
        var result = await Mediator.Send(new GetHealthcareCenterProfileImageQuery(centerId), ct);
        return Ok(result);
    }

    /// <summary>Upload or replace a healthcare center's logo (center admin or super-admin).</summary>
    [HttpPut("healthcare-centers/{centerId:guid}")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(ProfileImageRules.MaxBytes)]
    public async Task<IActionResult> UploadCenterImage(Guid centerId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        await using var stream = file.OpenReadStream();
        var command = new UploadHealthcareCenterProfileImageCommand(
            centerId,
            stream,
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length);
        var result = await Mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>Remove a healthcare center's logo (center admin or super-admin).</summary>
    [HttpDelete("healthcare-centers/{centerId:guid}")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteCenterImage(Guid centerId, CancellationToken ct)
    {
        await Mediator.Send(new DeleteHealthcareCenterProfileImageCommand(centerId), ct);
        return NoContent();
    }
}
