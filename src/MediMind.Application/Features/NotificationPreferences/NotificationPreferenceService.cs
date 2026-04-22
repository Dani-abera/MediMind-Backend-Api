using AutoMapper;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.NotificationPreferences;

public interface INotificationPreferenceService
{
    Task<NotificationPreferenceResponseDto> GetOrCreateAsync(Guid userId, CancellationToken ct = default);
    Task<NotificationPreferenceResponseDto> UpdateAsync(Guid userId, UpdateNotificationPreferenceDto dto, CancellationToken ct = default);
}

public class NotificationPreferenceService(
    INotificationPreferenceRepository repository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : INotificationPreferenceService
{
    public async Task<NotificationPreferenceResponseDto> GetOrCreateAsync(Guid userId, CancellationToken ct = default)
    {
        var pref = await repository.GetByUserIdAsync(userId, ct);
        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(userId);
            await repository.AddAsync(pref, ct);
            await unitOfWork.SaveChangesAsync(ct);
        }
        return mapper.Map<NotificationPreferenceResponseDto>(pref);
    }

    public async Task<NotificationPreferenceResponseDto> UpdateAsync(Guid userId, UpdateNotificationPreferenceDto dto, CancellationToken ct = default)
    {
        var pref = await repository.GetByUserIdAsync(userId, ct);
        if (pref is null)
        {
            pref = NotificationPreference.CreateDefault(userId);
            await repository.AddAsync(pref, ct);
        }

        pref.Update(dto.AppointmentRemindersPush, dto.AppointmentRemindersSms,
            dto.QueueUpdates, dto.HealthPredictionReady,
            dto.MedicationReminders, dto.PromotionalEmails);

        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<NotificationPreferenceResponseDto>(pref);
    }
}
