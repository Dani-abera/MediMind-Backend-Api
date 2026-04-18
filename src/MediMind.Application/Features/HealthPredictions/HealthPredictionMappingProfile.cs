using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.HealthPredictions;

public class HealthPredictionMappingProfile : Profile
{
    public HealthPredictionMappingProfile()
    {
        CreateMap<HealthPrediction, HealthPredictionSummaryDto>()
            .ForCtorParam(nameof(HealthPredictionSummaryDto.PredictionId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.PredictionDate), opt => opt.MapFrom(src => src.PredictionDate))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.DiabetesCategory), opt => opt.MapFrom(src => src.DiabetesCategory.ToString()))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.HypertensionCategory), opt => opt.MapFrom(src => src.HypertensionCategory.ToString()))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.CvdCategory), opt => opt.MapFrom(src => src.CvdCategory.ToString()))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.Confidence), opt => opt.MapFrom(src => src.Confidence))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.DataPointsUsed), opt => opt.MapFrom(src => src.DataPointsUsed))
            .ForCtorParam(nameof(HealthPredictionSummaryDto.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt));
    }
}
