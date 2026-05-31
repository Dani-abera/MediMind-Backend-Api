using MediatR;
using MediMind.Application.Features.ProfileImages;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Center logo management under /api/v1/healthcare-centers/{id}/logo.
/// The legacy /api/v1/profile-images/healthcare-centers/{id} endpoints
/// are kept with HTTP 301 redirects for one release cycle.
/// </summary>
[ApiController]
[Route("api/v1/healthcare-centers")]
public class CenterLogoController(IMediator mediator, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Get a healthcare center's logo URL (public).</summary>
    [Tags("Public")]
    [HttpGet("{centerId:guid}/logo")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetLogo(Guid centerId, CancellationToken ct)
    {
        var result = await mediator.Send(new GetHealthcareCenterProfileImageQuery(centerId), ct);
        return Ok(result);
    }

    /// <summary>Upload or replace a healthcare center's logo (center admin or super-admin).</summary>
    [Tags("Admin — Center")]
    [HttpPut("{centerId:guid}/logo")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(typeof(ProfileImageUrlDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(ProfileImageRules.MaxBytes)]
    public async Task<IActionResult> UploadLogo(Guid centerId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (currentUser.TenantId.HasValue && currentUser.TenantId.Value != centerId && currentUser.UserType != "SuperAdmin")
            return Forbid();

        await using var stream = file.OpenReadStream();
        var command = new UploadHealthcareCenterProfileImageCommand(
            centerId, stream, file.FileName,
            file.ContentType ?? "application/octet-stream", file.Length);
        var result = await mediator.Send(command, ct);
        return Ok(result);
    }

    /// <summary>
    /// Upload a center image by type ("logo" or "cover") — used by the admin portal branding page.
    /// Both types are stored as the center's profile image URL (single storage field).
    /// </summary>
    [Tags("Admin — Center")]
    [HttpPost("{centerId:guid}/upload-image")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [RequestSizeLimit(ProfileImageRules.MaxBytes)]
    public async Task<IActionResult> UploadImage(
        Guid centerId,
        [FromForm] IFormFile file,
        [FromForm] string imageType,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "Image file is required." });

        if (currentUser.TenantId.HasValue && currentUser.TenantId.Value != centerId && currentUser.UserType != "SuperAdmin")
            return Forbid();

        await using var stream = file.OpenReadStream();
        var command = new UploadHealthcareCenterProfileImageCommand(
            centerId, stream, file.FileName,
            file.ContentType ?? "application/octet-stream", file.Length);
        var result = await mediator.Send(command, ct);
        return Ok(new { url = result.ProfileImageUrl });
    }

    /// <summary>Remove a healthcare center's logo (center admin or super-admin).</summary>
    [Tags("Admin — Center")]
    [HttpDelete("{centerId:guid}/logo")]
    [Authorize(Policy = "AdminOrSuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteLogo(Guid centerId, CancellationToken ct)
    {
        if (currentUser.TenantId.HasValue && currentUser.TenantId.Value != centerId && currentUser.UserType != "SuperAdmin")
            return Forbid();

        await mediator.Send(new DeleteHealthcareCenterProfileImageCommand(centerId), ct);
        return NoContent();
    }
}

/// <summary>
/// Backward-compatible redirect shim for the old /profile-images/healthcare-centers/{id} URLs.
/// Returns HTTP 301 to the canonical /healthcare-centers/{id}/logo path.
/// Remove in the release after AdminExtensions ships.
/// </summary>
[ApiController]
[Route("api/v1/profile-images")]
[ApiExplorerSettings(IgnoreApi = true)]
public class CenterLogoRedirectController : ControllerBase
{
    [HttpGet("healthcare-centers/{centerId:guid}")]
    public IActionResult RedirectGet(Guid centerId) =>
        RedirectPermanent($"/api/v1/healthcare-centers/{centerId}/logo");

    [HttpPut("healthcare-centers/{centerId:guid}")]
    public IActionResult RedirectPut(Guid centerId) =>
        RedirectPermanent($"/api/v1/healthcare-centers/{centerId}/logo");

    [HttpDelete("healthcare-centers/{centerId:guid}")]
    public IActionResult RedirectDelete(Guid centerId) =>
        RedirectPermanent($"/api/v1/healthcare-centers/{centerId}/logo");
}
