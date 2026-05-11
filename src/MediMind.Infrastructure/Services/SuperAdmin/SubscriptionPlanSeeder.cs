using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.SuperAdmin;

/// <summary>Seeds the subscription_plans table on startup if empty.</summary>
public sealed class SubscriptionPlanSeeder(
    IServiceScopeFactory scopeFactory,
    ILogger<SubscriptionPlanSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediMindDbContext>();

        if (await db.SubscriptionPlans.AnyAsync(ct)) return;

        var plans = new[]
        {
            SubscriptionPlan.Create(
                "Trial", SubscriptionPlanTier.Trial,
                monthlyPrice: 0m, yearlyPrice: 0m,
                maxDoctors: 3, maxAppointmentsPerDay: 20,
                features: ["Basic appointment management", "Patient registration", "Queue management"],
                description: "30-day free trial. Explore all core features."),

            SubscriptionPlan.Create(
                "Basic", SubscriptionPlanTier.Basic,
                monthlyPrice: 999m, yearlyPrice: 9990m,
                maxDoctors: 10, maxAppointmentsPerDay: 50,
                features: ["Appointment management", "Patient registration", "Queue management", "SMS reminders", "Basic analytics"],
                description: "For small clinics and health centers."),

            SubscriptionPlan.Create(
                "Standard", SubscriptionPlanTier.Standard,
                monthlyPrice: 2499m, yearlyPrice: 24990m,
                maxDoctors: 30, maxAppointmentsPerDay: 150,
                features: ["Everything in Basic", "Video consultations", "Health records", "AI health predictions", "Prescription management", "Revenue reports"],
                description: "For medium-sized hospitals and multi-specialty clinics."),

            SubscriptionPlan.Create(
                "Premium", SubscriptionPlanTier.Premium,
                monthlyPrice: 4999m, yearlyPrice: 49990m,
                maxDoctors: int.MaxValue, maxAppointmentsPerDay: int.MaxValue,
                features: ["Everything in Standard", "Unlimited doctors", "Unlimited appointments", "Priority support", "Custom integrations", "Advanced analytics", "Multi-branch management"],
                description: "For large hospital chains and healthcare networks."),
        };

        db.SubscriptionPlans.AddRange(plans);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("✅ Seeded {Count} subscription plans.", plans.Length);
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
