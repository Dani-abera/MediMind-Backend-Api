using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/centers")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Centers")]
public class SuperAdminCentersController(
    ISuperAdminCenterService centerService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List all healthcare centers with filters.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] string? name,
        [FromQuery] string? city,
        [FromQuery] SubscriptionStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await centerService.GetAllCentersAsync(new SuperAdminCenterQueryDto(name, city, status, page, pageSize), ct);
        return Ok(result);
    }

    /// <summary>List centers awaiting approval.</summary>
    [HttpGet("pending")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var result = await centerService.GetPendingCentersAsync(ct);
        return Ok(result);
    }

    /// <summary>List active (subscribed) centers.</summary>
    [HttpGet("active")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await centerService.GetActiveCentersAsync(ct);
        return Ok(result);
    }

    /// <summary>List suspended centers.</summary>
    [HttpGet("suspended")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetSuspended(CancellationToken ct)
    {
        var result = await centerService.GetSuspendedCentersAsync(ct);
        return Ok(result);
    }

    /// <summary>List rejected centers.</summary>
    [HttpGet("rejected")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRejected(CancellationToken ct)
    {
        var result = await centerService.GetRejectedCentersAsync(ct);
        return Ok(result);
    }

    /// <summary>List expired-subscription centers.</summary>
    [HttpGet("expired")]
    [ProducesResponseType(typeof(IReadOnlyList<SuperAdminCenterSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExpired(CancellationToken ct)
    {
        var result = await centerService.GetExpiredCentersAsync(ct);
        return Ok(result);
    }

    /// <summary>Get a single center by ID.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var result = await centerService.GetCenterAsync(id, ct);
        return Ok(result);
    }

    /// <summary>Approve a pending center, placing it in Trial status.</summary>
    [HttpPost("{id:guid}/approve")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveCenterDto dto, CancellationToken ct)
    {
        var result = await centerService.ApproveCenterAsync(id, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Reject a center registration with a reason.</summary>
    [HttpPost("{id:guid}/reject")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectCenterDto dto, CancellationToken ct)
    {
        var result = await centerService.RejectCenterAsync(id, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Suspend an active center.</summary>
    [HttpPost("{id:guid}/suspend")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Suspend(Guid id, [FromBody] SuspendCenterDto dto, CancellationToken ct)
    {
        var result = await centerService.SuspendCenterAsync(id, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Reactivate a suspended center.</summary>
    [HttpPost("{id:guid}/reactivate")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Reactivate(Guid id, CancellationToken ct)
    {
        var result = await centerService.ReactivateCenterAsync(id, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Verify the Chapa subscription payment and activate the center (combined verify + approve action).</summary>
    [HttpPost("{id:guid}/verify-payment")]
    [ProducesResponseType(typeof(SuperAdminCenterSummaryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> VerifyPayment(Guid id, CancellationToken ct)
    {
        var result = await centerService.VerifySubscriptionPaymentAsync(id, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Soft-delete a center (GDPR NFR-009). Irreversible.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await centerService.SoftDeleteCenterAsync(id, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Regenerate the queue for a center on a given date.</summary>
    [HttpPost("{id:guid}/queue/regenerate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> RegenerateQueue(
        Guid id,
        [FromBody] RegenerateQueueDto dto,
        [FromServices] Application.Features.Queue.IQueueService queueService,
        CancellationToken ct)
    {
        if (dto.ConfirmationMessage != "CONFIRM_REGENERATE")
            return BadRequest(new { error = "Confirmation message must be 'CONFIRM_REGENERATE'." });

        var date = dto.Date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var result = await queueService.GenerateQueueForCenterAsync(id, date, currentUser.UserId);
        return Ok(result);
    }
}
