using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.NotificationPreferences;

public class NotificationPreferenceMappingProfile : Profile
{
    public NotificationPreferenceMappingProfile()
    {
        CreateMap<NotificationPreference, NotificationPreferenceResponseDto>()
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.Id), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.UserId), opt => opt.MapFrom(src => src.UserId))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.AppointmentRemindersPush), opt => opt.MapFrom(src => src.AppointmentRemindersPush))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.AppointmentRemindersSms), opt => opt.MapFrom(src => src.AppointmentRemindersSms))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.QueueUpdates), opt => opt.MapFrom(src => src.QueueUpdates))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.HealthPredictionReady), opt => opt.MapFrom(src => src.HealthPredictionReady))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.MedicationReminders), opt => opt.MapFrom(src => src.MedicationReminders))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.PromotionalEmails), opt => opt.MapFrom(src => src.PromotionalEmails))
            .ForCtorParam(nameof(NotificationPreferenceResponseDto.UpdatedAt), opt => opt.MapFrom(src => src.UpdatedAt));
    }
}
