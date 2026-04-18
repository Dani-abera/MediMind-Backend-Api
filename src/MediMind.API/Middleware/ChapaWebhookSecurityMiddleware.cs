using System.Text;
using MediMind.Application.Features.Payments;
using Serilog;

namespace MediMind.API.Middleware;

public sealed class ChapaWebhookSecurityMiddleware(
    RequestDelegate next,
    IChapaWebhookValidator validator,
    IChapaConfiguration configuration)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.Equals("/api/v1/payments/webhook", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var signature = context.Request.Headers["Chapa-Signature"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(signature))
        {
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new { status = "ignored", reason = "missing signature" });
            return;
        }

        context.Request.EnableBuffering();
        using var reader = new StreamReader(context.Request.Body, Encoding.UTF8, leaveOpen: true);
        var payload = await reader.ReadToEndAsync();
        context.Request.Body.Position = 0;

        Log.Information("Chapa webhook received from {IP}: {Payload}", context.Connection.RemoteIpAddress, payload);
        if (!validator.ValidateSignature(payload, signature, configuration.WebhookSecret))
        {
            Log.Warning("Chapa webhook rejected due to invalid signature from {IP}", context.Connection.RemoteIpAddress);
            context.Response.StatusCode = StatusCodes.Status200OK;
            await context.Response.WriteAsJsonAsync(new { status = "ignored", reason = "invalid signature" });
            return;
        }

        await next(context);
    }
}
