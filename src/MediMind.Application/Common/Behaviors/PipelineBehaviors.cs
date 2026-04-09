using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Common.Behaviors;

// ─── Validation Pipeline Behavior ─────────────────────────────────────────────

/// <summary>
/// Runs all FluentValidation validators for a command/query before the handler executes.
/// Throws ValidationException if any rule fails — maps to HTTP 400 at API layer.
/// </summary>
public class ValidationBehavior<TRequest, TResponse>(
    IEnumerable<IValidator<TRequest>> validators,
    ILogger<ValidationBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        if (!validators.Any()) return await next(cancellationToken);

        logger.LogDebug("Validating {RequestType}", typeof(TRequest).Name);

        var context = new ValidationContext<TRequest>(request);
        var validationResults = await Task.WhenAll(
            validators.Select(v => v.ValidateAsync(context, cancellationToken)));

        var failures = validationResults
            .Where(r => r.Errors.Count != 0)
            .SelectMany(r => r.Errors)
            .ToList();

        if (failures.Count != 0)
        {
            logger.LogWarning("Validation failed for {RequestType}: {Errors}",
                typeof(TRequest).Name,
                string.Join("; ", failures.Select(f => f.ErrorMessage)));
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}

// ─── Logging Pipeline Behavior ────────────────────────────────────────────────

/// <summary>
/// Logs all incoming requests and their execution time.
/// Warns if a request takes longer than 1 second.
/// </summary>
public class LoggingBehavior<TRequest, TResponse>(
    ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var response = await next(cancellationToken);
        sw.Stop();

        if (sw.ElapsedMilliseconds > 1000)
            logger.LogWarning("Slow request {RequestName} took {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);
        else
            logger.LogInformation("Handled {RequestName} in {ElapsedMs}ms", requestName, sw.ElapsedMilliseconds);

        return response;
    }
}
