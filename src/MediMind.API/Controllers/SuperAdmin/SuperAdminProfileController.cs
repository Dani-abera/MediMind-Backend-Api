using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/profile")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Profile")]
public class SuperAdminProfileController(
    ISuperAdminProfileService profileService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Get the logged-in super admin's own profile.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SuperAdminProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile(CancellationToken ct)
    {
        var result = await profileService.GetProfileAsync(currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Update the logged-in super admin's profile (fullName, dateOfBirth, gender).</summary>
    [HttpPut]
    [ProducesResponseType(typeof(SuperAdminProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateSuperAdminProfileDto dto, CancellationToken ct)
    {
        var result = await profileService.UpdateProfileAsync(currentUser.UserId, dto, ct);
        return Ok(result);
    }
}
