using MediMind.Application.Features.Admin;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.Audit;

public class AuditLoggerService(
    IServiceScopeFactory scopeFactory,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLoggerService> logger) : IAuditLogger
{
    public async Task LogAsync(
        string action,
        Guid? userId,
        string userType,
        Guid? centerId,
        string? entityType = null,
        Guid? entityId = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var ctx = httpContextAccessor.HttpContext;
            var ipAddress = ctx?.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            var userAgent = ctx?.Request.Headers.UserAgent.ToString() ?? "unknown";

            var entry = AuditLog.Create(action, userId, userType, centerId, entityType, entityId, ipAddress, userAgent, metadata);

            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            await repo.AppendAsync(entry, ct);
            await uow.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to write audit log for action {Action}", action);
            // Audit failure must never break the main flow
        }
    }
}
