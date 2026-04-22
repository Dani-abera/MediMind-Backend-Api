using AutoMapper;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.MedicalHistory;

public interface IMedicalHistoryService
{
    Task<MedicalHistoryResponseDto?> GetAsync(Guid patientId, CancellationToken ct = default);
    Task<MedicalHistoryResponseDto> UpsertAsync(Guid patientId, UpsertMedicalHistoryDto dto, CancellationToken ct = default);
    Task ClearAsync(Guid patientId, CancellationToken ct = default);
}

public class MedicalHistoryService(
    IPatientMedicalHistoryRepository repository,
    IUnitOfWork unitOfWork,
    IMapper mapper) : IMedicalHistoryService
{
    public async Task<MedicalHistoryResponseDto?> GetAsync(Guid patientId, CancellationToken ct = default)
    {
        var history = await repository.GetByPatientIdAsync(patientId, ct);
        return history is null ? null : mapper.Map<MedicalHistoryResponseDto>(history);
    }

    public async Task<MedicalHistoryResponseDto> UpsertAsync(Guid patientId, UpsertMedicalHistoryDto dto, CancellationToken ct = default)
    {
        var history = await repository.GetByPatientIdAsync(patientId, ct);

        if (history is null)
        {
            history = PatientMedicalHistory.Create(patientId);
            history.Update(dto.ChronicConditions, dto.Allergies, dto.CurrentMedications,
                dto.BloodType, dto.Smoker, dto.AlcoholConsumption, dto.FamilyHistory);
            await repository.AddAsync(history, ct);
        }
        else
        {
            history.Update(dto.ChronicConditions, dto.Allergies, dto.CurrentMedications,
                dto.BloodType, dto.Smoker, dto.AlcoholConsumption, dto.FamilyHistory);
            await repository.UpdateAsync(history, ct);
        }

        await unitOfWork.SaveChangesAsync(ct);
        return mapper.Map<MedicalHistoryResponseDto>(history);
    }

    public async Task ClearAsync(Guid patientId, CancellationToken ct = default)
    {
        var history = await repository.GetByPatientIdAsync(patientId, ct);
        if (history is null)
        {
            history = PatientMedicalHistory.Create(patientId);
            await repository.AddAsync(history, ct);
        }
        else
        {
            history.Clear();
            await repository.UpdateAsync(history, ct);
        }
        await unitOfWork.SaveChangesAsync(ct);
    }
}
