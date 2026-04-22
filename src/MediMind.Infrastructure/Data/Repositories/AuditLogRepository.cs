using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Data.Repositories;

public class AuditLogRepository(MediMindDbContext context) : IAuditLogRepository
{
    public async Task AppendAsync(AuditLog entry, CancellationToken ct = default)
    {
        await context.AuditLogs.AddAsync(entry, ct);
    }

    public async Task<PagedResult<AuditLog>> QueryAsync(Guid centerId, AuditLogFilterDto query, CancellationToken ct = default)
    {
        var q = context.AuditLogs.Where(l => l.CenterId == centerId).AsQueryable();
        return await ApplyFiltersAndPage(q, query, ct);
    }

    public async Task<PagedResult<AuditLog>> QueryGlobalAsync(AuditLogFilterDto query, CancellationToken ct = default)
    {
        var q = context.AuditLogs.AsQueryable();
        return await ApplyFiltersAndPage(q, query, ct);
    }

    private static async Task<PagedResult<AuditLog>> ApplyFiltersAndPage(IQueryable<AuditLog> q, AuditLogFilterDto query, CancellationToken ct)
    {
        if (query.StartDate.HasValue)
            q = q.Where(l => l.Timestamp >= query.StartDate.Value.ToDateTime(TimeOnly.MinValue));
        if (query.EndDate.HasValue)
            q = q.Where(l => l.Timestamp < query.EndDate.Value.AddDays(1).ToDateTime(TimeOnly.MinValue));
        if (query.UserId.HasValue)
            q = q.Where(l => l.UserId == query.UserId.Value);
        if (!string.IsNullOrWhiteSpace(query.Action))
            q = q.Where(l => l.Action == query.Action);

        var total = await q.CountAsync(ct);
        var items = await q
            .OrderByDescending(l => l.Timestamp)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        return new PagedResult<AuditLog>(items, query.Page, query.PageSize, total);
    }
}
