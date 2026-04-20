using AspNetCoreRateLimit;
using Hangfire;
using MediMind.Application;
using MediMind.Application.Auth;
using MediMind.Application.Features.HealthRecords;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.Queue;
using MediMind.Domain.Common.Interfaces;
using MediMind.Infrastructure;
using MediMind.Infrastructure.Data;
using MediMind.Infrastructure.Jobs;
using MediMind.Infrastructure.Services.HealthPredictions;
using MediMind.API.OpenApi;
using MediMind.API.Middleware;
using MediMind.Infrastructure.SignalR;
using MediMind.Infrastructure.Services.Notifications;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Polly;
using Polly.Extensions.Http;
using Scalar.AspNetCore;
using Serilog;
using Serilog.Events;

// ─── Serilog Bootstrap Logger ────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting MediMind API...");

    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog ─────────────────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, cfg) => cfg
        .ReadFrom.Configuration(ctx.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/medimind-.txt", rollingInterval: RollingInterval.Day));

    // ─── Application + Infrastructure ────────────────────────────────────────
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);
    FirebaseBootstrap.Initialize(builder.Configuration);
    builder.Services.AddHostedService<AppointmentReminderService>();
    builder.Services.AddHostedService<MedicationReminderHostedService>();
    builder.Services.AddHostedService<DailyQueueGenerationService>();
    builder.Services.AddSignalR(options =>
    {
        options.EnableDetailedErrors = true;
    });
    // Explicit registration for health records service (also discoverable from Program.cs).
    builder.Services.AddScoped<IHealthRecordService, HealthRecordService>();
    builder.Services.Configure<MlServiceOptions>(builder.Configuration.GetSection(MlServiceOptions.SectionName));
    builder.Services
        .AddHttpClient<IMlServiceClient, MlServiceClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<MlServiceOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    builder.Services.Configure<TestOtpOptions>(builder.Configuration.GetSection(TestOtpOptions.SectionName));
    

    // ─── Controllers ─────────────────────────────────────────────────────────
    builder.Services.AddControllers()
        .AddJsonOptions(o =>
        {
            o.JsonSerializerOptions.MaxDepth = 128;
            o.JsonSerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
        });
    // OpenAPI schema generator uses the minimal-API JsonOptions, not MVC's
    builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(o =>
    {
        o.SerializerOptions.MaxDepth = 128;
        o.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
    });
    builder.Services.AddEndpointsApiExplorer();
    builder.Services.AddMediMindOpenApi();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(options =>
    {
        options.AddPolicy("MediMindPolicy", policy =>
        {
            var allowedOrigins = builder.Configuration
                .GetSection("Cors:AllowedOrigins")
                .Get<string[]>() ?? new[] { "http://localhost:3000", "http://localhost:5173" };

            if (builder.Environment.IsDevelopment())
            {
                policy.SetIsOriginAllowed(_ => true)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
            else
            {
                policy.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            }
        });
    });

    // ─── Rate Limiting (NFR-007: 100 req/min/user) ────────────────────────────
    builder.Services.AddMemoryCache();
    builder.Services.Configure<AspNetCoreRateLimit.IpRateLimitOptions>(
        builder.Configuration.GetSection("IpRateLimiting"));
    builder.Services.AddInMemoryRateLimiting();
    builder.Services.AddSingleton<AspNetCoreRateLimit.IRateLimitConfiguration,
        AspNetCoreRateLimit.RateLimitConfiguration>();

    // ─── Authorization Policies ───────────────────────────────────────────────
    builder.Services.AddAuthorization(options =>
    {
        options.AddPolicy("PatientOnly", p => p.RequireClaim("user_type", "Patient"));
        options.AddPolicy("DoctorOnly", p => p.RequireClaim("user_type", "Doctor"));
        options.AddPolicy("PatientOrDoctor", p => p.RequireClaim("user_type", "Patient", "Doctor"));
        options.AddPolicy("AdminOnly", p => p.RequireClaim("user_type", "Admin"));
        options.AddPolicy("SuperAdminOnly", p => p.RequireClaim("user_type", "SuperAdmin"));
        options.AddPolicy("DoctorOrAdmin", p => p.RequireClaim("user_type", "Doctor", "Admin"));
        options.AddPolicy("PatientOrAdmin", p => p.RequireClaim("user_type", "Patient", "Admin"));
        options.AddPolicy("HealthcareStaff", p => p.RequireClaim("user_type", "Doctor", "Admin", "SuperAdmin"));
        options.AddPolicy("AdminOrSuperAdmin", p => p.RequireClaim("user_type", "Admin", "SuperAdmin"));
    });

    var app = builder.Build();

    // ─── Auto-migrate Database on Startup ─────────────────────────────────────
    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<MediMindDbContext>();
        await db.Database.MigrateAsync();
    }

    // ─── Middleware Pipeline ──────────────────────────────────────────────────
    app.UseSerilogRequestLogging();

    var uploadsRoot = Path.Combine(app.Environment.ContentRootPath, "uploads");
    Directory.CreateDirectory(uploadsRoot);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadsRoot),
        RequestPath = "/uploads"
    });

    var storageRoot = Path.Combine(
        app.Environment.ContentRootPath,
        (app.Configuration["Storage:PrescriptionPdfPath"] ?? "storage/prescriptions/").TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
    var storageParent = Path.GetDirectoryName(storageRoot.TrimEnd(Path.DirectorySeparatorChar));
    if (storageParent is not null)
    {
        Directory.CreateDirectory(storageRoot);
        app.UseStaticFiles(new StaticFileOptions
        {
            FileProvider = new PhysicalFileProvider(storageParent),
            RequestPath = "/storage"
        });
    }

    app.UseHttpsRedirection();
    app.UseCors("MediMindPolicy");
    app.UseIpRateLimiting();
    app.UseMiddleware<ChapaWebhookSecurityMiddleware>();

    app.UseAuthentication();
    app.UseMiddleware<TenantValidationMiddleware>();
    app.UseAuthorization();

    // Global exception handler (maps domain exceptions to correct HTTP codes)
    app.UseExceptionHandler("/error");

    app.MapControllers();

    // Example — development-only minimal API (alternative to NotificationsController.TestPush):
    // if (app.Environment.IsDevelopment())
    // {
    //     app.MapPost("/api/v1/notifications/test-push-minimal",
    //             async (Guid userId, string title, string body, INotificationService notifications, CancellationToken ct) =>
    //         {
    //             await notifications.SendPushAsync(userId, title, body, null, ct);
    //             return Results.NoContent();
    //         })
    //         .RequireAuthorization("SuperAdminOnly");
    // }

    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options
                .WithTitle("MediMind API")
                .WithTheme(ScalarTheme.Saturn)
                .AddDocument(
                    MediMindOpenApiExtensions.DocumentName,
                    "MediMind API v1",
                    $"/openapi/{MediMindOpenApiExtensions.DocumentName}.json")
                .AddPreferredSecuritySchemes("Bearer")
                .AddHttpAuthentication("Bearer", _ => { });
        });
    }

    // ─── SignalR Hub Endpoint ─────────────────────────────────────────────────
    app.MapHub<QueueHub>("/hubs/queue");
    app.MapHub<VideoConsultationHub>("/hubs/video");

    // ─── Health Check Endpoint ────────────────────────────────────────────────
    app.MapHealthChecks("/health");

    // ─── Hangfire Dashboard (admin only in production) ─────────────────────────
    app.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        IsReadOnlyFunc = _ => !app.Environment.IsDevelopment()
    });

    // ─── Schedule Recurring Jobs ──────────────────────────────────────────────
    // Daily queue generation at 06:00 AM Ethiopian time (03:00 AM UTC)
    RecurringJob.AddOrUpdate<IQueueGenerationJob>(
        "generate-daily-queues",
        job => job.GenerateDailyQueuesAsync(DateOnly.FromDateTime(DateTime.UtcNow), CancellationToken.None),
        "0 3 * * *",  // 03:00 AM UTC = 06:00 AM EAT
        new RecurringJobOptions { TimeZone = TimeZoneInfo.Utc });

    // Appointment reminders: 24h and 2h before (check every 30 minutes)
    RecurringJob.AddOrUpdate<IAppointmentReminderJob>(
        "send-appointment-reminders",
        job => job.SendRemindersAsync(CancellationToken.None),
        "*/30 * * * *"); // Every 30 minutes

    app.Lifetime.ApplicationStarted.Register(() =>
    {
        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;
        if (addresses is { Count: > 0 })
        {
            Log.Information("MediMind API listening on {Urls}", string.Join(", ", addresses));
            if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Staging"))
            {
                foreach (var address in addresses)
                {
                    Log.Information("Scalar API reference available at {ScalarUrl}", $"{address.TrimEnd('/')}/scalar/v1");
                }
            }
            return;
        }

        var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
        if (!string.IsNullOrWhiteSpace(envUrls))
        {
            Log.Information("MediMind API started (ASPNETCORE_URLS: {Urls})", envUrls);
            return;
        }

        Log.Information(
            "MediMind API started. URLs not bound yet in log; use launch profile URLs or set ASPNETCORE_URLS (see Properties/launchSettings.json).");
    });

    await app.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "MediMind API terminated unexpectedly.");
    return 1;
}
finally
{
    await Log.CloseAndFlushAsync();
}

return 0;
