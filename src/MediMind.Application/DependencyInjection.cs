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
        services.AddScoped<Features.Queue.IQueueService, Features.Queue.QueueService>();
        services.AddScoped<Features.CenterManagement.IHealthcareCenterService, Features.CenterManagement.HealthcareCenterService>();
        services.AddScoped<Features.CenterManagement.IAnalyticsService, Features.CenterManagement.AnalyticsService>();

        return services;
    }
}
