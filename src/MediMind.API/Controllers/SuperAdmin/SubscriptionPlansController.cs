using MediMind.Application.Features.SuperAdmin;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

/// <summary>
/// Public read + SuperAdmin write endpoints for subscription plan master data.
/// </summary>
[ApiController]
[Route("api/v1/subscription-plans")]
public sealed class SubscriptionPlansController(ISubscriptionPlanService planService) : ControllerBase
{
    /// <summary>List all active subscription plans (public).</summary>
    [Tags("Subscription Plans")]
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IReadOnlyList<SubscriptionPlanDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<SubscriptionPlanDto>>> GetAll(
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default)
    {
        var plans = await planService.GetAllPlansAsync(includeInactive, ct);
        return Ok(plans);
    }

    /// <summary>Get a subscription plan by ID.</summary>
    [Tags("Subscription Plans")]
    [HttpGet("{planId:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SubscriptionPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionPlanDto>> GetById(Guid planId, CancellationToken ct)
    {
        var plan = await planService.GetPlanByIdAsync(planId, ct);
        return Ok(plan);
    }

    /// <summary>Create a new subscription plan (SuperAdmin only).</summary>
    [Tags("SuperAdmin — Subscription Plans")]
    [HttpPost]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(SubscriptionPlanDto), StatusCodes.Status201Created)]
    public async Task<ActionResult<SubscriptionPlanDto>> Create(
        [FromBody] CreateSubscriptionPlanDto dto,
        CancellationToken ct)
    {
        var plan = await planService.CreatePlanAsync(dto, ct);
        return Created($"/api/v1/subscription-plans/{plan.PlanId}", plan);
    }

    /// <summary>Update pricing and limits for an existing plan (SuperAdmin only).</summary>
    [Tags("SuperAdmin — Subscription Plans")]
    [HttpPut("{planId:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(typeof(SubscriptionPlanDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<SubscriptionPlanDto>> Update(
        Guid planId,
        [FromBody] UpdateSubscriptionPlanDto dto,
        CancellationToken ct)
    {
        var plan = await planService.UpdatePlanAsync(planId, dto, ct);
        return Ok(plan);
    }

    /// <summary>Deactivate a plan so it no longer appears to new subscribers (SuperAdmin only).</summary>
    [Tags("SuperAdmin — Subscription Plans")]
    [HttpDelete("{planId:guid}")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Deactivate(Guid planId, CancellationToken ct)
    {
        await planService.DeactivatePlanAsync(planId, ct);
        return NoContent();
    }
}
