namespace MediMind.Application.Features.PrescriptionTemplates;

public record CreatePrescriptionTemplateDto(
    string Name,
    string? Description,
    string? Diagnosis,
    string Medications,
    string? LabTests,
    string? FollowUpInstructions);

public record UpdatePrescriptionTemplateDto(
    string Name,
    string? Description,
    string? Diagnosis,
    string Medications,
    string? LabTests,
    string? FollowUpInstructions);

public record PrescriptionTemplateResponseDto(
    Guid TemplateId,
    Guid DoctorId,
    string Name,
    string? Description,
    string? Diagnosis,
    string Medications,
    string? LabTests,
    string? FollowUpInstructions,
    int UseCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public record CreatePrescriptionFromTemplateDto(
    Guid TemplateId,
    Guid AppointmentId,
    string? DiagnosisOverride,
    string? MedicationsOverride,
    string? LabTestsOverride,
    string? FollowUpInstructionsOverride,
    string? SpecialInstructions);
