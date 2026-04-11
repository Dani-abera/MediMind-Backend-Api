using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace MediMind.API.OpenApi;

/// <summary>
/// Registers OpenAPI document generation (Microsoft.AspNetCore.OpenApi) with MediMind-specific metadata.
/// Scalar consumes the generated JSON; Swashbuckle is not used.
/// </summary>
public static class MediMindOpenApiExtensions
{
    public const string DocumentName = "v1";

    public static IServiceCollection AddMediMindOpenApi(this IServiceCollection services)
    {
        services.AddOpenApi(DocumentName, options =>
        {
            options.AddOperationTransformer<MediMindAuthOperationTransformer>();
            options.AddDocumentTransformer<MediMindOpenApiDocumentTransformer>();
        });

        return services;
    }
}

/// <summary>
/// Rich top-level API description, contact, license, and JWT Bearer scheme for Scalar.
/// </summary>
internal sealed class MediMindOpenApiDocumentTransformer(IAuthenticationSchemeProvider schemeProvider)
    : IOpenApiDocumentTransformer
{
    public async Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        document.Info = new OpenApiInfo
        {
            Title = "MediMind API",
            Version = "v1",
            Description = """
                **MediMind** is the backend for an AI-assisted hospital platform: appointments, queues, health records, payments (Chapa), video visits, prescriptions, and center analytics.

                ## Base URL
                All REST endpoints are under **`/api/v1/...`** unless noted otherwise.

                ## Authentication
                1. Call a public auth route (e.g. register, verify OTP, or refresh token).
                2. Copy the **access token** from the response.
                3. In Scalar, open **Authenticate**, choose **Bearer**, paste the token **without** the word `Bearer` (Scalar adds the prefix).

                JWTs include a **`user_type`** claim used by policies: **Patient**, **Doctor**, **Admin**, **SuperAdmin**. Many routes also require a **tenant** (healthcare center) context for admins.

                ## Realtime
                - **SignalR** hub: `/hubs/queue` (pass JWT as `?access_token=` query for WebSockets).
                - **Hangfire dashboard**: `/hangfire` (read-only outside Development).

                ## Rate limiting
                IP-based limits apply (see `IpRateLimiting` in configuration). Auth routes have stricter limits.

                ## Errors
                Validation and domain errors are returned as RFC 7807 **Problem Details** via the global exception handler.
                """,
            Contact = new OpenApiContact
            {
                Name = "MediMind Team",
                Email = "support@medimind.et"
            }
        };

        var schemes = await schemeProvider.GetAllSchemesAsync();
        if (!schemes.Any(s => s.Name == "Bearer"))
            return;

        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??=
            new Dictionary<string, IOpenApiSecurityScheme>(StringComparer.Ordinal);

        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            In = ParameterLocation.Header,
            Description =
                "Paste the **access token** only (no `Bearer ` prefix). Obtain it from verify-OTP, refresh-token, or login flows."
        };
    }
}

/// <summary>
/// Marks operations as requiring Bearer auth unless explicitly <see cref="AllowAnonymousAttribute"/>.
/// </summary>
internal sealed class MediMindAuthOperationTransformer : IOpenApiOperationTransformer
{
    public Task TransformAsync(
        OpenApiOperation operation,
        OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken)
    {
        var descriptor = context.Description.ActionDescriptor as ControllerActionDescriptor;
        var metadata = descriptor?.EndpointMetadata ?? [];

        if (metadata.OfType<AllowAnonymousAttribute>().Any())
            return Task.CompletedTask;

        if (!metadata.OfType<AuthorizeAttribute>().Any())
            return Task.CompletedTask;

        operation.Security ??= [];
        operation.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", context.Document)] = []
        });

        operation.Responses ??= new OpenApiResponses();
        if (!operation.Responses.ContainsKey("401"))
        {
            operation.Responses.Add("401",
                new OpenApiResponse { Description = "Missing or invalid JWT (expired token, wrong signature, or no Authorization header)." });
        }

        if (!operation.Responses.ContainsKey("403"))
        {
            operation.Responses.Add("403",
                new OpenApiResponse
                {
                    Description =
                        "Authenticated but forbidden: wrong **user_type** claim, missing center (**tenant**) context, or resource belongs to another center."
                });
        }

        return Task.CompletedTask;
    }
}
