using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.HealthRecordAttachments;

public class HealthRecordAttachmentMappingProfile : Profile
{
    public HealthRecordAttachmentMappingProfile()
    {
        CreateMap<HealthRecordAttachment, HealthRecordAttachmentResponseDto>()
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.AttachmentId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.HealthRecordId), opt => opt.MapFrom(src => src.HealthRecordId))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.FileName), opt => opt.MapFrom(src => src.FileName))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.FileType), opt => opt.MapFrom(src => src.FileType))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.FileSizeBytes), opt => opt.MapFrom(src => src.FileSizeBytes))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.StoragePath), opt => opt.MapFrom(src => src.StoragePath))
            .ForCtorParam(nameof(HealthRecordAttachmentResponseDto.UploadedAt), opt => opt.MapFrom(src => src.UploadedAt));
    }
}
