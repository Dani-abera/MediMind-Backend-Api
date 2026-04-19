namespace MediMind.Application.Features.Prescriptions;

public record MedicationDto(
    string Name,
    string Dosage,
    string Frequency,
    string Duration,
    string? Instructions);

public record CreatePrescriptionDto(
    Guid AppointmentId,
    string Diagnosis,
    List<MedicationDto> Medications,
    List<string>? LabTests,
    string? FollowUpInstructions,
    string? SpecialInstructions,
    DateOnly? ExpiryDate);

public record PrescriptionResponseDto(
    Guid PrescriptionId,
    Guid AppointmentId,
    Guid PatientId,
    Guid DoctorId,
    Guid CenterId,
    DateOnly IssueDate,
    DateOnly? ExpiryDate,
    string Status,
    string Diagnosis,
    List<MedicationDto> Medications,
    List<string>? LabTests,
    string? FollowUpInstructions,
    string? SpecialInstructions,
    string QrCodeBase64,
    string? PdfUrl,
    bool IsExpired,
    string DoctorName,
    string PatientName,
    string CenterName);

public record PrescriptionVerificationDto(
    bool IsValid,
    string PatientNameMasked,
    string DoctorName,
    string CenterName,
    string IssuedOn,
    string Status,
    string? WarningMessage);
