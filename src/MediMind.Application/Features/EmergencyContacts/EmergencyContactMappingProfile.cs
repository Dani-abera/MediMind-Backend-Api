using AutoMapper;
using MediMind.Domain.Entities;

namespace MediMind.Application.Features.EmergencyContacts;

public class EmergencyContactMappingProfile : Profile
{
    public EmergencyContactMappingProfile()
    {
        CreateMap<EmergencyContact, EmergencyContactResponseDto>()
            .ForCtorParam(nameof(EmergencyContactResponseDto.ContactId), opt => opt.MapFrom(src => src.Id))
            .ForCtorParam(nameof(EmergencyContactResponseDto.PatientId), opt => opt.MapFrom(src => src.PatientId))
            .ForCtorParam(nameof(EmergencyContactResponseDto.FullName), opt => opt.MapFrom(src => src.FullName))
            .ForCtorParam(nameof(EmergencyContactResponseDto.Relationship), opt => opt.MapFrom(src => src.Relationship))
            .ForCtorParam(nameof(EmergencyContactResponseDto.PhoneNumber), opt => opt.MapFrom(src => src.PhoneNumber))
            .ForCtorParam(nameof(EmergencyContactResponseDto.IsPrimary), opt => opt.MapFrom(src => src.IsPrimary))
            .ForCtorParam(nameof(EmergencyContactResponseDto.CreatedAt), opt => opt.MapFrom(src => src.CreatedAt));
    }
}
