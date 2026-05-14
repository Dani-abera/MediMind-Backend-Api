using System.Net.Http.Headers;
using System.Text;
using Hangfire;
using Hangfire.MemoryStorage;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.Payments;
using MediMind.Application.Features.VideoConsultations;
using MediMind.Domain.Common.Interfaces;
using MediMind.Infrastructure.Data;
using MediMind.Infrastructure.Data.Repositories;
using MediMind.Infrastructure.Jobs;
using MediMind.Infrastructure.Services.Auth;
using MediMind.Infrastructure.Services.HealthPredictions;
using MediMind.Infrastructure.Services.ML;
using MediMind.Infrastructure.Services.Notifications;
using MediMind.Infrastructure.Services.Payment;
using MediMind.Infrastructure.Services.Pdf;
using MediMind.Infrastructure.Services.Prescriptions;
using MediMind.Infrastructure.Services.Audit;
using MediMind.Application.Features.SuperAdmin;
using MediMind.Infrastructure.Services.SuperAdmin;
using MediMind.Infrastructure.Services.Storage;
using MediMind.Infrastructure.SignalR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

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
                config.GetConnectionString("DefaultConnection")));

        // ─── Unit of Work ────────────────────────────────────────────────────
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<MediMindDbContext>());

        // ─── Repositories ────────────────────────────────────────────────────
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPatientRepository, PatientRepository>();
        services.AddScoped<IDoctorRepository, DoctorRepository>();
        services.AddScoped<IOtpVerificationRepository, OtpVerificationRepository>();
        services.AddScoped<IHealthcareCenterRepository, HealthcareCenterRepository>();
        services.AddScoped<IAppointmentRepository, AppointmentRepository>();
        services.AddScoped<IDoctorScheduleRepository, DoctorScheduleRepository>();
        services.AddScoped<IQueueRepository, QueueRepository>();
        services.AddScoped<IHealthRecordRepository, HealthRecordRepository>();
        services.AddScoped<IHealthPredictionRepository, HealthPredictionRepository>();
        services.AddScoped<IPaymentRepository, PaymentRepository>();
        services.AddScoped<IVideoConsultationRepository, VideoConsultationRepository>();
        services.AddScoped<IPrescriptionRepository, PrescriptionRepository>();
        
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));

        services.Configure<PrescriptionStorageOptions>(config.GetSection(PrescriptionStorageOptions.SectionName));
        services.Configure<PrescriptionVerificationOptions>(config.GetSection(PrescriptionVerificationOptions.SectionName));

        services.Configure<GeezSmsOptions>(o =>
        {
            var s = config.GetSection(GeezSmsOptions.SectionName);
            o.ApiKey = s["ApiKey"] ?? config["GeezsMS:ApiKey"] ?? string.Empty;
            o.SenderId = s["SenderId"] ?? "MediMind";
            o.BaseUrl = s["BaseUrl"] ?? "https://api.geezsms.com/api/v1";
        });

        services.AddHttpClient<GeezSmsClient>((sp, client) =>
        {
            var o = sp.GetRequiredService<IOptions<GeezSmsOptions>>().Value;
            var baseUrl = string.IsNullOrWhiteSpace(o.BaseUrl) ? "https://api.geezsms.com/api/v1" : o.BaseUrl.TrimEnd('/');
            client.BaseAddress = new Uri(baseUrl + "/");
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddScoped<FcmClient>();
        services.AddScoped<IUserDeviceTokenRepository, UserDeviceTokenRepository>();
        services.AddScoped<INotificationLogRepository, NotificationLogRepository>();
        services.AddScoped<IMedicationReminderRepository, MedicationReminderRepository>();
        services.AddScoped<IPatientMedicalHistoryRepository, PatientMedicalHistoryRepository>();
        services.AddScoped<IEmergencyContactRepository, EmergencyContactRepository>();
        services.AddScoped<IHealthRecordAttachmentRepository, HealthRecordAttachmentRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IFavoriteRepository, FavoriteRepository>();
        services.AddScoped<INotificationPreferenceRepository, NotificationPreferenceRepository>();
        services.AddScoped<IWaitlistSubscriptionRepository, WaitlistSubscriptionRepository>();
        services.AddScoped<IPrescriptionTemplateRepository, PrescriptionTemplateRepository>();
        services.AddScoped<IAppointmentNoteRepository, AppointmentNoteRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IDoctorInvitationRepository, DoctorInvitationRepository>();
        services.AddScoped<IScheduleExceptionRepository, ScheduleExceptionRepository>();

        services.AddScoped<ISmsService, GeezSmsService>();
        services.AddScoped<IPushNotificationService, FirebasePushNotificationService>();
        services.AddScoped<IEmailService, SendGridEmailService>();
        services.AddScoped<INotificationService, MediMindNotificationService>();
        services.AddScoped<IPdfService, PrescriptionPdfService>();
        services.AddScoped<IQrCodeService, QrCodeService>();
        services.AddScoped<IPrescriptionFileStorage, PrescriptionLocalFileStorage>();

        services.Configure<CloudinaryOptions>(config.GetSection(CloudinaryOptions.SectionName));
        services.AddScoped<LocalUploadStorageService>();
        services.AddScoped<IStorageService>(sp =>
        {
            var o = sp.GetRequiredService<IOptions<CloudinaryOptions>>().Value;
            if (!string.IsNullOrWhiteSpace(o.CloudName)
                && !string.IsNullOrWhiteSpace(o.ApiKey)
                && !string.IsNullOrWhiteSpace(o.ApiSecret))
                return ActivatorUtilities.CreateInstance<CloudinaryStorageService>(sp);
            return sp.GetRequiredService<LocalUploadStorageService>();
        });

        // ─── Auth Services ───────────────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IJwtService>(sp => (IJwtService)sp.GetRequiredService<ITokenService>());
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordService, PasswordService>();
        services.AddScoped<IOtpService, OtpService>();
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

        // Python Flask ML Service (legacy named client + new typed client consumes same settings)
        services.Configure<MlServiceOptions>(config.GetSection(MlServiceOptions.SectionName));
        services.AddHttpClient("MlService", client =>
        {
            client.BaseAddress = new Uri(config["MlService:BaseUrl"] ?? "http://localhost:5001");
            client.Timeout = TimeSpan.FromSeconds(config.GetValue("MlService:TimeoutSeconds", 30));
        });

        services.AddHttpClient("SendGrid", (sp, client) =>
        {
            client.BaseAddress = new Uri("https://api.sendgrid.com/");
            client.Timeout = TimeSpan.FromSeconds(30);
            var cfg = sp.GetRequiredService<IConfiguration>();
            var apiKey = cfg["SendGrid:ApiKey"];
            if (!string.IsNullOrWhiteSpace(apiKey))
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        });

        services.Configure<ChapaOptions>(config.GetSection(ChapaOptions.SectionName));
        services.Configure<PaymentSettings>(config.GetSection(PaymentSettings.SectionName));
        services.AddHttpClient<IChapaClient, ChapaClient>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<ChapaOptions>>().Value;
            client.BaseAddress = new Uri("https://api.chapa.co/v1/");
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        });

        // ─── External Services ───────────────────────────────────────────────
        services.AddScoped<IMlPredictionService, MlPredictionService>();
        services.AddScoped<MediMind.Domain.Common.Interfaces.IPaymentService, ChapaPaymentService>();
        services.AddSingleton<IChapaWebhookValidator, ChapaWebhookValidator>();
        services.AddSingleton<IChapaConfiguration, ChapaConfigurationAdapter>();
        services.AddSingleton<IPaymentConfiguration, PaymentConfigurationAdapter>();
        services.AddScoped<ISubscriptionPlanService, SubscriptionPlanService>();
        services.AddScoped<Application.Features.Admin.IAuditLogger, Services.Audit.AuditLoggerService>();
        services.AddHostedService<SuperAdminSeeder>();
        services.AddHostedService<SubscriptionPlanSeeder>();
        services.AddHostedService<SubscriptionExpiryBackgroundService>();
        services.AddScoped<IQueueHubService, QueueHubService>();
        services.AddScoped<IVideoConsultationHubNotifier, VideoConsultationHubNotifier>();

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
            .UseMemoryStorage());

        services.AddHangfireServer(options =>
        {
            options.WorkerCount = 5;
            options.Queues = new[] { "queue_generation", "reminders", "notifications", "default" };
        });

        // Hangfire job implementations (so recurring jobs can be resolved via DI)
        services.AddScoped<IAppointmentReminderJob, AppointmentReminderJob>();
        services.AddScoped<IAppointmentSmsRetryJob, AppointmentSmsRetryJob>();
        services.AddScoped<IQueueGenerationJob, QueueGenerationJob>();

        // ─── Health Checks ───────────────────────────────────────────────────
        services.AddHealthChecks()
            .AddHangfire(options => { options.MinimumAvailableServers = 1; }, name: "hangfire");

        return services;
    }
}
