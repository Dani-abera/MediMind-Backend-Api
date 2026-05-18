using FluentValidation;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var (status, title) = MapException(exception);

        if (status >= 500)
            logger.LogError(exception, "Unhandled server error on {Method} {Path}", httpContext.Request.Method, httpContext.Request.Path);
        else
            logger.LogWarning("{ExceptionType} on {Method} {Path}: {Message}",
                exception.GetType().Name, httpContext.Request.Method, httpContext.Request.Path, exception.Message);

        httpContext.Response.StatusCode = status;
        httpContext.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = exception.Message,
            Instance = httpContext.Request.Path,
            Type = "https://tools.ietf.org/html/rfc7807"
        };

        if (exception is ValidationException ve)
        {
            problem.Extensions["errors"] = ve.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }

        await httpContext.Response.WriteAsJsonAsync(problem, cancellationToken: cancellationToken);
        return true;
    }

    private static (int status, string title) MapException(Exception ex) => ex switch
    {
        ValidationException       => (400, "Validation Failed"),
        DomainException           => (400, "Business Rule Violation"),
        NotFoundException         => (404, "Resource Not Found"),
        ForbiddenException        => (403, "Access Denied"),
        TenantIsolationException  => (403, "Tenant Isolation Violation"),
        UnauthorizedException     => (403, "Access Denied"),
        ServiceUnavailableException => (503, "Service Unavailable"),
        PaymentException          => (402, "Payment Error"),
        _                         => (500, "Internal Server Error")
    };
}
