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

        await next(context);
    }
}
