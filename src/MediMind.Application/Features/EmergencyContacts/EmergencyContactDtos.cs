using MediMind.Domain.Enums;

namespace MediMind.Application.Features.EmergencyContacts;

public record EmergencyContactResponseDto(
    Guid ContactId,
    Guid PatientId,
    string FullName,
    ContactRelationship Relationship,
    string PhoneNumber,
    bool IsPrimary,
    DateTime CreatedAt);

public record CreateEmergencyContactDto(
    string FullName,
    ContactRelationship Relationship,
    string PhoneNumber,
    bool IsPrimary);

public record UpdateEmergencyContactDto(
    string FullName,
    ContactRelationship Relationship,
    string PhoneNumber);
