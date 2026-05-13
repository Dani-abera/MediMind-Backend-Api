using AutoMapper;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using MediMind.Application.Common.Behaviors;

namespace MediMind.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = Assembly.GetExecutingAssembly();

        // MediatR — all handlers, event handlers from this assembly
        services.AddMediatR(cfg =>
        {
            cfg.RegisterServicesFromAssembly(assembly);
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
            cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        });

        // FluentValidation — all validators from this assembly
        services.AddValidatorsFromAssembly(assembly);

        // AutoMapper — all profiles from this assembly
        services.AddAutoMapper(_ => { }, assembly);

        // Health records application service
        services.AddScoped<Features.HealthRecords.IHealthRecordService, Features.HealthRecords.HealthRecordService>();
        services.AddScoped<Features.HealthPredictions.IHealthPredictionService, Features.HealthPredictions.HealthPredictionService>();
        services.AddScoped<Features.HealthPredictions.IHealthFeatureEngineeringService, Features.HealthPredictions.HealthFeatureEngineeringService>();
        services.AddScoped<Features.Appointments.IAppointmentAvailabilityService, Features.Appointments.AppointmentAvailabilityService>();
        services.AddScoped<Features.Appointments.IBookingValidationService, Features.Appointments.BookingValidationService>();
        services.AddScoped<Features.Appointments.IAppointmentService, Features.Appointments.AppointmentService>();
        services.AddScoped<Features.Appointments.IWaitlistService, Features.Appointments.WaitlistService>();
        services.AddScoped<Features.Queue.IQueueService, Features.Queue.QueueService>();
        services.AddScoped<Features.CenterManagement.IHealthcareCenterService, Features.CenterManagement.HealthcareCenterService>();
        services.AddScoped<Features.CenterManagement.IAnalyticsService, Features.CenterManagement.AnalyticsService>();
        services.AddScoped<Features.Payments.IPaymentService, Features.Payments.PaymentService>();
        services.AddScoped<Features.VideoConsultations.IVideoConsultationService, Features.VideoConsultations.VideoConsultationService>();
        services.AddScoped<Features.Prescriptions.IPrescriptionService, Features.Prescriptions.PrescriptionService>();
        services.AddScoped<Features.Auth.IPatientAuthService, Features.Auth.PatientAuthService>();
        services.AddScoped<Features.Auth.IDoctorAuthService, Features.Auth.DoctorAuthService>();
        services.AddScoped<Features.Auth.IAdminAuthService, Features.Auth.AdminAuthService>();
        services.AddScoped<Features.Auth.ISuperAdminAuthService, Features.Auth.SuperAdminAuthService>();

        services.AddScoped<Features.MedicalHistory.IMedicalHistoryService, Features.MedicalHistory.MedicalHistoryService>();
        services.AddScoped<Features.EmergencyContacts.IEmergencyContactService, Features.EmergencyContacts.EmergencyContactService>();
        services.AddScoped<Features.HealthRecordAttachments.IHealthRecordAttachmentService, Features.HealthRecordAttachments.HealthRecordAttachmentService>();
        services.AddScoped<Features.Reviews.IReviewService, Features.Reviews.ReviewService>();
        services.AddScoped<Features.Favorites.IFavoriteService, Features.Favorites.FavoriteService>();
        services.AddScoped<Features.NotificationPreferences.INotificationPreferenceService, Features.NotificationPreferences.NotificationPreferenceService>();
        services.AddScoped<Features.Doctors.IDoctorProfileService, Features.Doctors.DoctorProfileService>();
        services.AddScoped<Features.Doctors.IDoctorPatientAccessChecker, Features.Doctors.DoctorPatientAccessChecker>();
        services.AddScoped<Features.PrescriptionTemplates.IPrescriptionTemplateService, Features.PrescriptionTemplates.PrescriptionTemplateService>();
        services.AddScoped<Features.AppointmentNotes.IAppointmentNoteService, Features.AppointmentNotes.AppointmentNoteService>();

        services.AddScoped<Features.Admin.IAdminManagementService, Features.Admin.AdminManagementService>();
        services.AddScoped<Features.Admin.IPatientDirectoryService, Features.Admin.PatientDirectoryService>();
        services.AddScoped<Features.Admin.ITodayDashboardService, Features.Admin.TodayDashboardService>();
        services.AddScoped<Features.Admin.IRevenueService, Features.Admin.RevenueService>();
        services.AddScoped<Features.Admin.IBulkOperationsService, Features.Admin.BulkOperationsService>();
        services.AddScoped<Features.Admin.IAuditLogService, Features.Admin.AuditLogService>();

        services.AddScoped<Features.SuperAdmin.ISuperAdminCenterService, Features.SuperAdmin.SuperAdminCenterService>();
        services.AddScoped<Features.SuperAdmin.ISuperAdminSubscriptionService, Features.SuperAdmin.SuperAdminSubscriptionService>();
        services.AddScoped<Features.SuperAdmin.ISuperAdminDoctorService, Features.SuperAdmin.SuperAdminDoctorService>();
        services.AddScoped<Features.SuperAdmin.ISuperAdminUserService, Features.SuperAdmin.SuperAdminUserService>();
        services.AddScoped<Features.SuperAdmin.ISuperAdminPlatformService, Features.SuperAdmin.SuperAdminPlatformService>();
        // ISubscriptionPlanService implemented in Infrastructure (needs DbContext); registered there.

        return services;
    }
}
