using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.HealthRecords;

public class HealthRecordMappingProfile : Profile
{
    public HealthRecordMappingProfile()
    {
        CreateMap<UpdateHealthRecordDto, CreateHealthRecordDto>();

        CreateMap<HealthRecord, HealthRecordResponseDto>()
            .ForCtorParam(nameof(HealthRecordResponseDto.RecordId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(HealthRecordResponseDto.PatientId), opt => opt.MapFrom(src => src.PatientId))
            .ForCtorParam(nameof(HealthRecordResponseDto.RecordDate), opt => opt.MapFrom(src => src.RecordDate))
            .ForCtorParam(nameof(HealthRecordResponseDto.RecordTime), opt => opt.MapFrom(src => src.RecordTime))
            .ForCtorParam(nameof(HealthRecordResponseDto.SystolicBp), opt => opt.MapFrom(src => src.SystolicBp))
            .ForCtorParam(nameof(HealthRecordResponseDto.DiastolicBp), opt => opt.MapFrom(src => src.DiastolicBp))
            .ForCtorParam(nameof(HealthRecordResponseDto.GlucoseLevel), opt => opt.MapFrom(src => src.GlucoseLevel))
            .ForCtorParam(nameof(HealthRecordResponseDto.Weight), opt => opt.MapFrom(src => src.Weight))
            .ForCtorParam(nameof(HealthRecordResponseDto.Height), opt => opt.MapFrom(src => src.Height))
            .ForCtorParam(nameof(HealthRecordResponseDto.Temperature), opt => opt.MapFrom(src => src.Temperature))
            .ForCtorParam(nameof(HealthRecordResponseDto.HeartRate), opt => opt.MapFrom(src => src.HeartRate))
            .ForCtorParam(nameof(HealthRecordResponseDto.OxygenSaturation), opt => opt.MapFrom(src => src.OxygenSaturation))
            .ForCtorParam(nameof(HealthRecordResponseDto.RespiratoryRate), opt => opt.MapFrom(src => src.RespiratoryRate))
            .ForCtorParam(nameof(HealthRecordResponseDto.Notes), opt => opt.MapFrom(src => src.Notes))
            .ForCtorParam(nameof(HealthRecordResponseDto.RecordedBy), opt => opt.MapFrom(src => src.RecordedBy))
            .ForCtorParam(nameof(HealthRecordResponseDto.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt))
            .ForCtorParam(nameof(HealthRecordResponseDto.Bmi), opt => opt.MapFrom(src => src.Bmi));

        CreateMap<HealthRecord, HealthRecordSummaryDto>()
            .ForCtorParam(nameof(HealthRecordSummaryDto.RecordId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(HealthRecordSummaryDto.RecordDate), opt => opt.MapFrom(src => src.RecordDate))
            .ForCtorParam(nameof(HealthRecordSummaryDto.SystolicBp), opt => opt.MapFrom(src => src.SystolicBp))
            .ForCtorParam(nameof(HealthRecordSummaryDto.DiastolicBp), opt => opt.MapFrom(src => src.DiastolicBp))
            .ForCtorParam(nameof(HealthRecordSummaryDto.GlucoseLevel), opt => opt.MapFrom(src => src.GlucoseLevel))
            .ForCtorParam(nameof(HealthRecordSummaryDto.Weight), opt => opt.MapFrom(src => src.Weight))
            .ForCtorParam(nameof(HealthRecordSummaryDto.Temperature), opt => opt.MapFrom(src => src.Temperature))
            .ForCtorParam(nameof(HealthRecordSummaryDto.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt));
    }
}
