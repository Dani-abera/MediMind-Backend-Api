namespace MediMind.Application.Features.EmergencyContacts;

public record EmergencyContactResponseDto(
    Guid ContactId,
    Guid PatientId,
    string FullName,
    string Relationship,
    string PhoneNumber,
    bool IsPrimary,
    DateTime CreatedAt);

public record CreateEmergencyContactDto(
    string FullName,
    string Relationship,
    string PhoneNumber,
    bool IsPrimary);

public record UpdateEmergencyContactDto(
    string FullName,
    string Relationship,
    string PhoneNumber);
