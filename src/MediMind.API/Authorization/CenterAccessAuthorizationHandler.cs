using Microsoft.AspNetCore.Authorization;

namespace MediMind.API.Authorization;

/// <summary>
/// Verifies the center_id JWT claim matches the route {id} parameter.
/// Inject as a policy requirement or call explicitly in service layer.
/// This handler provides the infrastructure — center validation is also
/// enforced in service layer via EnsureAdminBelongsToCenter.
/// </summary>
public class CenterAccessRequirement : IAuthorizationRequirement
{
    public static readonly CenterAccessRequirement Instance = new();
}

public class CenterAccessAuthorizationHandler
    : AuthorizationHandler<CenterAccessRequirement, Guid>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        CenterAccessRequirement requirement,
        Guid centerId)
    {
        var userType = context.User.FindFirst("user_type")?.Value;
        if (userType == "SuperAdmin")
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (userType != "Admin")
            return Task.CompletedTask;

        var tenantClaim = context.User.FindFirst("tenant_id")?.Value;
        if (Guid.TryParse(tenantClaim, out var tenantId) && tenantId == centerId)
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
