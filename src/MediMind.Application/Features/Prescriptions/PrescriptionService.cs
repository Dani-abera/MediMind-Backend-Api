using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MediMind.Application.Features.Prescriptions;

public class PrescriptionService(
    IAppointmentRepository appointmentRepository,
    IPrescriptionRepository prescriptionRepository,
    IPdfService pdfService,
    IPrescriptionFileStorage prescriptionFileStorage,
    IQrCodeService qrCodeService,
    IUnitOfWork unitOfWork,
    IServiceScopeFactory scopeFactory,
    ILogger<PrescriptionService> logger) : IPrescriptionService
{
    public async Task<PrescriptionResponseDto> CreatePrescriptionAsync(
        CreatePrescriptionDto dto,
        Guid doctorId,
        CancellationToken ct = default)
    {
        var appointment = await appointmentRepository.GetByIdAsync(dto.AppointmentId)
                          ?? throw new NotFoundException(nameof(Appointment), dto.AppointmentId);

        if (appointment.DoctorId != doctorId)
            throw new ForbiddenException("You can only issue prescriptions for your own appointments.");

        if (appointment.Status != AppointmentStatus.Completed &&
            appointment.Status != AppointmentStatus.InProgress)
            throw new DomainException(
                "Prescriptions can only be issued for in-progress or completed appointments.");

        var existing = await prescriptionRepository.GetByAppointmentAsync(dto.AppointmentId, ct);
        if (existing is not null)
            throw new DomainException("A prescription already exists for this appointment.");

        var medications = dto.Medications
            .Select(m => new MedicationItem(m.Name, m.Dosage, m.Frequency, m.Duration, m.Instructions))
            .ToList();

        var prescription = Prescription.Issue(
            dto.AppointmentId,
            appointment.PatientId,
            doctorId,
            appointment.CenterId,
            dto.Diagnosis,
            medications,
            dto.LabTests,
            dto.FollowUpInstructions,
            dto.SpecialInstructions,
            dto.ExpiryDate);

        var qrPayload = qrCodeService.BuildQrPayloadString(prescription.Id, prescription.IssueDate);
        var qrDataUrl = qrCodeService.GenerateQrCodeBase64(qrPayload);
        prescription.SetQrCode(qrDataUrl);

        await prescriptionRepository.CreateAsync(prescription, ct);
        await unitOfWork.SaveChangesAsync(ct);

        _ = GenerateAndStorePdfAsync(prescription.Id);

        return MapToResponse(
            prescription,
            appointment.Doctor?.FullName ?? string.Empty,
            appointment.Patient?.FullName ?? string.Empty,
            appointment.Center?.CenterName ?? string.Empty);
    }

    private async Task GenerateAndStorePdfAsync(Guid prescriptionId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var repo = scope.ServiceProvider.GetRequiredService<IPrescriptionRepository>();
            var pdf = scope.ServiceProvider.GetRequiredService<IPdfService>();
            var storage = scope.ServiceProvider.GetRequiredService<IPrescriptionFileStorage>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var rx = await repo.GetByIdForUpdateAsync(prescriptionId);
            if (rx is null)
                return;

            var bytes = await pdf.GeneratePrescriptionPdfAsync(
                rx,
                rx.Patient,
                rx.Doctor,
                rx.Center,
                rx.QrCode ?? string.Empty);

            var url = await storage.SavePrescriptionPdfAsync(prescriptionId, bytes);
            await repo.UpdatePdfUrlAsync(prescriptionId, url);
            await uow.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Background prescription PDF generation failed for {Id}", prescriptionId);
        }
    }

    public async Task<(byte[] Pdf, string FileName)> GetPrescriptionPdfAsync(
        Guid prescriptionId,
        Guid requesterId,
        string requesterType,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var rx = await prescriptionRepository.GetByIdWithDetailsAsync(prescriptionId, ct)
                 ?? throw new NotFoundException(nameof(Prescription), prescriptionId);

        EnsureCanAccess(rx, requesterId, requesterType, tenantId);

        var fromDisk = await prescriptionFileStorage.GetPrescriptionPdfAsync(prescriptionId, ct);
        if (fromDisk is not null)
            return (fromDisk, $"prescription_{prescriptionId}.pdf");

        var bytes = await pdfService.GeneratePrescriptionPdfAsync(
            rx,
            rx.Patient,
            rx.Doctor,
            rx.Center,
            rx.QrCode ?? string.Empty,
            ct);

        try
        {
            var url = await prescriptionFileStorage.SavePrescriptionPdfAsync(prescriptionId, bytes, ct);
            var tracked = await prescriptionRepository.GetByIdForUpdateAsync(prescriptionId, ct);
            tracked?.SetPrescriptionUrl(url);
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Could not persist generated PDF for prescription {Id}", prescriptionId);
        }

        return (bytes, $"prescription_{prescriptionId}.pdf");
    }

    public async Task<PrescriptionVerificationDto> VerifyPrescriptionAsync(
        Guid prescriptionId,
        string token,
        CancellationToken ct = default)
    {
        var rx = await prescriptionRepository.GetByIdWithDetailsAsync(prescriptionId, ct);
        if (rx is null)
            return new PrescriptionVerificationDto(
                false,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                "Prescription not found.");

        var validToken = qrCodeService.VerifyQrToken(prescriptionId, rx.IssueDate, token);
        if (!validToken)
            return new PrescriptionVerificationDto(
                false,
                MaskName(rx.Patient.FullName),
                rx.Doctor.FullName,
                rx.Center.CenterName,
                rx.IssueDate.ToString("yyyy-MM-dd"),
                rx.Status.ToString(),
                "Invalid verification token.");

        string? warning = null;
        if (rx.Status == PrescriptionStatus.Cancelled)
            warning = "This prescription has been cancelled.";
        else if (rx.Status == PrescriptionStatus.Expired || IsExpired(rx))
            warning = "This prescription has expired.";
        else if (rx.Status == PrescriptionStatus.Dispensed)
            warning = "This prescription has already been dispensed.";

        return new PrescriptionVerificationDto(
            true,
            MaskName(rx.Patient.FullName),
            rx.Doctor.FullName,
            rx.Center.CenterName,
            rx.IssueDate.ToString("yyyy-MM-dd"),
            rx.Status.ToString(),
            warning);
    }

    public async Task<IReadOnlyList<PrescriptionResponseDto>> ListForRequesterAsync(
        Guid requesterId,
        string requesterType,
        int page,
        int pageSize,
        CancellationToken ct = default)
    {
        IReadOnlyList<Prescription> list = requesterType switch
        {
            "Patient" => await prescriptionRepository.GetByPatientAsync(requesterId, page, pageSize, ct),
            "Doctor" => await prescriptionRepository.GetByDoctorAsync(requesterId, page, pageSize, ct),
            _ => throw new ForbiddenException("Unsupported role for this list.")
        };

        return list.Select(p => MapToResponse(
            p,
            p.Doctor?.FullName ?? "",
            p.Patient?.FullName ?? "",
            p.Center?.CenterName ?? "")).ToList();
    }

    public async Task<PrescriptionResponseDto?> GetDetailsAsync(
        Guid prescriptionId,
        Guid requesterId,
        string requesterType,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var rx = await prescriptionRepository.GetByIdWithDetailsAsync(prescriptionId, ct);
        if (rx is null)
            return null;

        if (!CanAccess(rx, requesterId, requesterType, tenantId))
            throw new ForbiddenException();

        return MapToResponse(rx, rx.Doctor.FullName, rx.Patient.FullName, rx.Center.CenterName);
    }

    public async Task<PrescriptionResponseDto?> GetByAppointmentAsync(
        Guid appointmentId,
        Guid requesterId,
        string requesterType,
        CancellationToken ct = default)
    {
        var rx = await prescriptionRepository.GetByAppointmentAsync(appointmentId, ct);
        if (rx is null)
            return null;

        if (requesterType == "Patient" && rx.PatientId != requesterId)
            throw new ForbiddenException();
        if (requesterType == "Doctor" && rx.DoctorId != requesterId)
            throw new ForbiddenException();

        return MapToResponse(rx, rx.Doctor.FullName, rx.Patient.FullName, rx.Center.CenterName);
    }

    public async Task MarkDispensedAsync(
        Guid prescriptionId,
        Guid requesterId,
        string requesterType,
        Guid? tenantId,
        CancellationToken ct = default)
    {
        var rx = await prescriptionRepository.GetByIdForUpdateAsync(prescriptionId, ct)
                 ?? throw new NotFoundException(nameof(Prescription), prescriptionId);

        var canDoctor = requesterType == "Doctor" && rx.DoctorId == requesterId;
        var canAdmin = requesterType == "Admin" && tenantId == rx.CenterId;
        if (!canDoctor && !canAdmin)
            throw new ForbiddenException("Only the issuing doctor or center admin can mark this dispensed.");

        if (rx.Status != PrescriptionStatus.Active)
            throw new DomainException("Only active prescriptions can be marked dispensed.");

        rx.MarkDispensed();
        await unitOfWork.SaveChangesAsync(ct);
    }

    private static void EnsureCanAccess(Prescription rx, Guid requesterId, string requesterType, Guid? tenantId)
    {
        if (!CanAccess(rx, requesterId, requesterType, tenantId))
            throw new ForbiddenException();
    }

    private static bool CanAccess(Prescription rx, Guid requesterId, string requesterType, Guid? tenantId) =>
        requesterType switch
        {
            "Patient" => rx.PatientId == requesterId,
            "Doctor" => rx.DoctorId == requesterId,
            "Admin" => tenantId == rx.CenterId,
            _ => false
        };

    private static PrescriptionResponseDto MapToResponse(
        Prescription p,
        string doctorName,
        string patientName,
        string centerName) =>
        new(
            p.Id,
            p.AppointmentId,
            p.PatientId,
            p.DoctorId,
            p.CenterId,
            p.IssueDate,
            p.ExpiryDate,
            p.Status.ToString(),
            p.Diagnosis,
            p.Medications.Select(m => new MedicationDto(m.Name, m.Dosage, m.Frequency, m.Duration, m.Instructions))
                .ToList(),
            p.LabTests.Count > 0 ? p.LabTests.ToList() : null,
            p.FollowUpInstructions,
            p.SpecialInstructions,
            p.QrCode ?? string.Empty,
            p.PrescriptionUrl,
            IsExpired(p),
            doctorName,
            patientName,
            centerName);

    private static bool IsExpired(Prescription p)
    {
        if (p.Status == PrescriptionStatus.Expired)
            return true;
        if (p.ExpiryDate is { } e)
            return e < DateOnly.FromDateTime(DateTime.UtcNow);
        return false;
    }

    private static string MaskName(string fullName)
    {
        var p = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (p.Length == 0)
            return "****";
        if (p.Length == 1)
            return $"{p[0][0]}***";
        return $"{p[0]} {p[^1][0]}.";
    }
}
