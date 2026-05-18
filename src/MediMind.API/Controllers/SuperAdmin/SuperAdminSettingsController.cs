using MediMind.Application.Features.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/settings")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Settings")]
public class SuperAdminSettingsController(IPlatformSettingsService settingsService) : ControllerBase
{
    /// <summary>Get platform-wide configuration settings.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PlatformSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await settingsService.GetAsync(ct);
        return Ok(result);
    }

    /// <summary>Update platform-wide configuration settings.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(PlatformSettingsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Update([FromBody] UpdatePlatformSettingsDto dto, CancellationToken ct)
    {
        var result = await settingsService.UpdateAsync(dto, ct);
        return Ok(result);
    }
}
