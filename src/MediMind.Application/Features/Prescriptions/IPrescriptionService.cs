namespace MediMind.Application.Features.Prescriptions;

public interface IPrescriptionService
{
    Task<PrescriptionResponseDto> CreatePrescriptionAsync(CreatePrescriptionDto dto, Guid doctorId, CancellationToken ct = default);
    Task<(byte[] Pdf, string FileName)> GetPrescriptionPdfAsync(Guid prescriptionId, Guid requesterId, string requesterType, Guid? tenantId, CancellationToken ct = default);
    Task<PrescriptionVerificationDto> VerifyPrescriptionAsync(Guid prescriptionId, string token, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionResponseDto>> ListForRequesterAsync(Guid requesterId, string requesterType, int page, int pageSize, CancellationToken ct = default);
    Task<PrescriptionResponseDto?> GetDetailsAsync(Guid prescriptionId, Guid requesterId, string requesterType, Guid? tenantId, CancellationToken ct = default);
    Task<PrescriptionResponseDto?> GetByAppointmentAsync(Guid appointmentId, Guid requesterId, string requesterType, CancellationToken ct = default);
    Task MarkDispensedAsync(Guid prescriptionId, Guid requesterId, string requesterType, Guid? tenantId, CancellationToken ct = default);
    Task RevokePrescriptionAsync(Guid prescriptionId, string reason, Guid requesterId, string requesterType, Guid? tenantId, CancellationToken ct = default);
}
