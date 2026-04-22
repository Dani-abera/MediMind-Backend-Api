using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MediMind.Infrastructure.Services.SuperAdmin;

public class SuperAdminSeeder(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SuperAdminSeeder> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["SuperAdmin:Email"];
        var password = configuration["SuperAdmin:Password"];

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning(
                "⚠️  No SuperAdmin seed credentials configured. " +
                "Set SuperAdmin:Email and SuperAdmin:Password in appsettings to create the initial SuperAdmin account.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MediMindDbContext>();
        var passwordService = scope.ServiceProvider.GetRequiredService<IPasswordService>();

        var exists = await db.Users.AnyAsync(u => u.Email == email.ToLowerInvariant(), cancellationToken);
        if (exists)
        {
            var hasSuperAdmin = await db.Users.AnyAsync(u => u.UserType == UserType.SuperAdmin, cancellationToken);
            if (!hasSuperAdmin)
                logger.LogWarning("⚠️  No SuperAdmin account found in the database. Register one manually.");
            return;
        }

        var superAdmin = new SuperAdminUser(
            email,
            "+251900000000",
            "System SuperAdmin",
            new DateOnly(1990, 1, 1),
            Gender.Other);

        superAdmin.SetPasswordHash(passwordService.HashPassword(password));
        superAdmin.Activate();

        await db.Users.AddAsync(superAdmin, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        logger.LogInformation("✅ SuperAdmin account seeded: {Email}", email);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
