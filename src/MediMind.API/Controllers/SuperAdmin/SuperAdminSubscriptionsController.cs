using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/centers/{centerId:guid}/subscription")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Subscriptions")]
public class SuperAdminSubscriptionsController(
    ISuperAdminSubscriptionService subscriptionService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List all center subscriptions (paginated).</summary>
    [HttpGet("/api/v1/super-admin/subscriptions")]
    [ProducesResponseType(typeof(PagedResult<SubscriptionDetailDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var result = await subscriptionService.GetAllSubscriptionsAsync(page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>Get current subscription details and history for a center.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(SubscriptionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid centerId, CancellationToken ct)
    {
        var result = await subscriptionService.GetSubscriptionAsync(centerId, ct);
        return Ok(result);
    }

    /// <summary>Set a new subscription plan with explicit start/end dates.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(SubscriptionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid centerId, [FromBody] UpdateSubscriptionDto dto, CancellationToken ct)
    {
        var result = await subscriptionService.UpdateSubscriptionAsync(centerId, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>Extend current subscription by N days.</summary>
    [HttpPost("extend")]
    [ProducesResponseType(typeof(SubscriptionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Extend(Guid centerId, [FromBody] ExtendSubscriptionDto dto, CancellationToken ct)
    {
        var result = await subscriptionService.ExtendSubscriptionAsync(centerId, dto, currentUser.UserId, ct);
        return Ok(result);
    }

    /// <summary>List full subscription change history for a center.</summary>
    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionHistoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> History(Guid centerId, CancellationToken ct)
    {
        var result = await subscriptionService.GetHistoryAsync(centerId, ct);
        return Ok(result);
    }
}
