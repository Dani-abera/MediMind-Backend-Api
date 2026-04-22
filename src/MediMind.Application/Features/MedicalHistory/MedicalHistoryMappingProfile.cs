using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.MedicalHistory;

public class MedicalHistoryMappingProfile : Profile
{
    public MedicalHistoryMappingProfile()
    {
        CreateMap<PatientMedicalHistory, MedicalHistoryResponseDto>()
            .ForCtorParam(nameof(MedicalHistoryResponseDto.HistoryId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.PatientId), opt => opt.MapFrom(src => src.PatientId))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.ChronicConditions), opt => opt.MapFrom(src => src.ChronicConditions))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.Allergies), opt => opt.MapFrom(src => src.Allergies))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.CurrentMedications), opt => opt.MapFrom(src => src.CurrentMedications))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.BloodType), opt => opt.MapFrom(src => src.BloodType))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.Smoker), opt => opt.MapFrom(src => src.Smoker))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.AlcoholConsumption), opt => opt.MapFrom(src => src.AlcoholConsumption))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.FamilyHistory), opt => opt.MapFrom(src => src.FamilyHistory))
            .ForCtorParam(nameof(MedicalHistoryResponseDto.UpdatedAt), opt => opt.MapFrom(src => src.UpdatedAt));
    }
}
