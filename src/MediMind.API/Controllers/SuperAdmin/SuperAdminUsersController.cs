using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/users")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Users")]
public class SuperAdminUsersController(
    ISuperAdminUserService userService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Search all platform users with optional filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SuperAdminUserSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search(
        [FromQuery] string? search,
        [FromQuery] UserType? userType,
        [FromQuery] UserStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await userService.SearchUsersAsync(new SuperAdminUserQueryDto(search, userType, status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>Get a user's full profile.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SuperAdminUserSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await userService.GetUserAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Suspend a user account (blocks login).</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendUserDto dto, CancellationToken ct)
    {
        await userService.SuspendUserAsync(id, dto, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Reactivate a suspended user account.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        await userService.ReactivateUserAsync(id, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Revoke the user's refresh token — forces logout on next token expiry.</summary>
    [HttpPost("{id:guid}/force-logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ForceLogout(Guid id, [FromBody] ForceLogoutDto dto, CancellationToken ct)
    {
        await userService.ForceLogoutAsync(id, dto, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Soft-delete a user for GDPR compliance (NFR-009). Cannot be undone.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, [FromBody] SoftDeleteUserDto dto, CancellationToken ct)
    {
        await userService.SoftDeleteUserAsync(id, dto, currentUser.UserId, ct);
        return NoContent();
    }
}
