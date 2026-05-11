using MediMind.Application.Features.SuperAdmin;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace MediMind.Infrastructure.Services.SuperAdmin;

public sealed class SubscriptionPlanService(MediMindDbContext db) : ISubscriptionPlanService
{
    public async Task<IReadOnlyList<SubscriptionPlanDto>> GetAllPlansAsync(bool includeInactive = false, CancellationToken ct = default)
    {
        var query = db.SubscriptionPlans.AsQueryable();
        if (!includeInactive) query = query.Where(p => p.IsActive);
        var plans = await query.OrderBy(p => p.Tier).ToListAsync(ct);
        return plans.Select(Map).ToList();
    }

    public async Task<SubscriptionPlanDto> GetPlanByIdAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);
        return Map(plan);
    }

    public async Task<SubscriptionPlanDto> CreatePlanAsync(CreateSubscriptionPlanDto dto, CancellationToken ct = default)
    {
        var nameConflict = await db.SubscriptionPlans.AnyAsync(p => p.Name == dto.Name, ct);
        if (nameConflict)
            throw new DomainException($"A subscription plan named '{dto.Name}' already exists.");

        var plan = SubscriptionPlan.Create(
            dto.Name, dto.Tier, dto.MonthlyPrice, dto.YearlyPrice,
            dto.MaxDoctors, dto.MaxAppointmentsPerDay, dto.Features, dto.Description);

        db.SubscriptionPlans.Add(plan);
        await db.SaveChangesAsync(ct);
        return Map(plan);
    }

    public async Task<SubscriptionPlanDto> UpdatePlanAsync(Guid planId, UpdateSubscriptionPlanDto dto, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);

        var updatedFeatures = dto.Features ?? plan.Features;
        plan.UpdatePricing(dto.MonthlyPrice, dto.YearlyPrice, dto.MaxDoctors, dto.MaxAppointmentsPerDay, dto.Description);
        await db.SaveChangesAsync(ct);
        return Map(plan);
    }

    public async Task DeactivatePlanAsync(Guid planId, CancellationToken ct = default)
    {
        var plan = await db.SubscriptionPlans.FindAsync([planId], ct)
            ?? throw new NotFoundException(nameof(SubscriptionPlan), planId);
        plan.Deactivate();
        await db.SaveChangesAsync(ct);
    }

    private static SubscriptionPlanDto Map(SubscriptionPlan p) =>
        new(p.Id, p.Name, p.Tier, p.MonthlyPrice, p.YearlyPrice,
            p.MaxDoctors, p.MaxAppointmentsPerDay, p.Features, p.Description, p.IsActive, p.CreatedAt);
}
