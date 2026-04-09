using Hangfire;
using Hangfire.PostgreSql;
using MediMind.Domain.Common.Interfaces;
using MediMind.Infrastructure.Data;
using MediMind.Infrastructure.Data.Repositories;
using MediMind.Infrastructure.Services.Auth;
using MediMind.Infrastructure.Services.ML;
using MediMind.Infrastructure.Services.Payment;
using MediMind.Infrastructure.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace MediMind.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration config)
    {
        // ─── PostgreSQL + EF Core 10 ─────────────────────────────────────────
        services.AddDbContext<MediMindDbContext>(options =>
            options.UseNpgsql(
                config.GetConnectionString("DefaultConnection"),
                npgsql =>
                {
                    npgsql.MigrationsAssembly(typeof(MediMindDbContext).Assembly.FullName);
                    npgsql.EnableRetryOnFailure(3);
                    npgsql.CommandTimeout(30);
                }));

        // ─── Unit of Work ────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MediMindDbContext>());

        // ─── Repositories ────────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IHealthcareCenterRepository, HealthcareCenterRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<IHealthRecordRepository, HealthRecordRepository>();
        services.AddScoped<IHealthPredictionRepository, HealthPredictionRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();

        // ─── Auth Services ───────────────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddSingleton<IOtpService, OtpService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        // ─── JWT Authentication ──────────────────────────────────────────────
        var secretKey = config["Jwt:SecretKey"]
            ?? throw new InvalidOperationException("Jwt:SecretKey not configured.");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidAudience = config["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(secretKey)),
                    ClockSkew = TimeSpan.Zero  // No leeway — 15 min is strict
                };

                // Support SignalR WebSocket auth via query string
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                            context.Token = accessToken;
                        return Task.CompletedTask;
                    }
                };
            });

        // ─── HTTP Clients ────────────────────────────────────────────────────

        // Python Flask ML Service
        services.AddHttpClient("MlService", client =>
        {
            client.BaseAddress = new Uri(config["MlService:BaseUrl"] ?? "http://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // Chapa Payment Gateway
        services.AddHttpClient("ChapaClient", client =>
        {
            client.BaseAddress = new Uri("https://api.chapa.co/");
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {config["Chapa:SecretKey"]}");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        // ─── External Services ───────────────────────────────────────────────
        services.AddScoped<IMlPredictionService, MlPredictionService>();
        services.AddScoped<IPaymentService, ChapaPaymentService>();
        services.AddScoped<IQueueHubService, QueueHubService>();

        // ─── SignalR (real-time queue updates) ───────────────────────────────
        services.AddSignalR(options =>
        {
            options.EnableDetailedErrors = true;
            options.MaximumReceiveMessageSize = 32 * 1024; // 32 KB
            options.KeepAliveInterval = TimeSpan.FromSeconds(30); // NFR-003: 30-second heartbeat
        });

        // ─── Hangfire Background Jobs ─────────────────────────────────────────
        services.AddHangfire(cfg => cfg
            .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UsePostgreSqlStorage(config.GetConnectionString("DefaultConnection")));

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 5;
            options.Queues = ["queue_generation", "reminders", "notifications", "default"];
        });

        // ─── Health Checks ───────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddNpgSql(config.GetConnectionString("DefaultConnection")!, name: "postgresql")
            .AddHangfire(options => { options.MinimumAvailableServers = 1; }, name: "hangfire");

        return services;
    }
}
