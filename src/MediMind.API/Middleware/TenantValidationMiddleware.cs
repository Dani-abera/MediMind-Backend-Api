using System.Security.Claims;

namespace MediMind.API.Middleware;

/// <summary>
/// Validates admin tenant access to healthcare center scoped routes.
/// </summary>
public class TenantValidationMiddleware(RequestDelegate next)
{
    /// <summary>
    /// Executes middleware validation.
    /// </summary>
    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value ?? string.Empty;

        if (path.StartsWith("/api/v1/healthcare-centers/", StringComparison.OrdinalIgnoreCase) &&
            context.User.Identity?.IsAuthenticated == true &&
            string.Equals(context.User.FindFirstValue("user_type"), "Admin", StringComparison.OrdinalIgnoreCase))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length >= 4 && Guid.TryParse(segments[3], out var centerIdFromRoute))
            {
                // Subscription payment is initiated right after center registration, before the
                // admin re-logs in to get a JWT with the new center_id claim. The service layer
                // already verifies ownership via DB (center.Admins.Any), so skip the claim check.
                var isSubscriptionPay = segments.Length >= 6
                    && string.Equals(segments[4], "subscription", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(segments[5], "pay", StringComparison.OrdinalIgnoreCase);

                if (!isSubscriptionPay)
                {
                    var centerClaim = context.User.FindFirst("center_id")?.Value
                                      ?? context.User.FindFirst("tenant_id")?.Value;

                    if (!Guid.TryParse(centerClaim, out var centerIdFromClaim) || centerIdFromClaim != centerIdFromRoute)
                    {
                        context.Response.StatusCode = StatusCodes.Status403Forbidden;
                        await context.Response.WriteAsJsonAsync(new { error = "Access denied to this healthcare center" });
                        return;
                    }
                }
            }
        }

        await next(context);
    }
}
