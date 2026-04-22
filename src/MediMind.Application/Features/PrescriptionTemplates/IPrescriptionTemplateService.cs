using MediMind.Application.Features.Prescriptions;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using System.Text.Json;

namespace MediMind.Application.Features.PrescriptionTemplates;

public interface IPrescriptionTemplateService
{
    Task<PrescriptionTemplateResponseDto> CreateAsync(Guid doctorId, CreatePrescriptionTemplateDto dto, CancellationToken ct = default);
    Task<IReadOnlyList<PrescriptionTemplateResponseDto>> ListAsync(Guid doctorId, CancellationToken ct = default);
    Task<PrescriptionTemplateResponseDto?> GetByIdAsync(Guid templateId, Guid doctorId, CancellationToken ct = default);
    Task<PrescriptionTemplateResponseDto> UpdateAsync(Guid templateId, Guid doctorId, UpdatePrescriptionTemplateDto dto, CancellationToken ct = default);
    Task DeleteAsync(Guid templateId, Guid doctorId, CancellationToken ct = default);
    Task<PrescriptionResponseDto> CreateFromTemplateAsync(Guid doctorId, CreatePrescriptionFromTemplateDto dto, CancellationToken ct = default);
}

public class PrescriptionTemplateService(
    IPrescriptionTemplateRepository templateRepository,
    IPrescriptionRepository prescriptionRepository,
    IAppointmentRepository appointmentRepository,
    IQrCodeService qrCodeService,
    IUnitOfWork unitOfWork) : IPrescriptionTemplateService
{
    public async Task<PrescriptionTemplateResponseDto> CreateAsync(Guid doctorId, CreatePrescriptionTemplateDto dto, CancellationToken ct = default)
    {
        var template = PrescriptionTemplate.Create(
            doctorId, dto.Name, dto.Description, dto.Diagnosis,
            dto.Medications, dto.LabTests, dto.FollowUpInstructions);

        var created = await templateRepository.CreateAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToDto(created);
    }

    public async Task<IReadOnlyList<PrescriptionTemplateResponseDto>> ListAsync(Guid doctorId, CancellationToken ct = default)
    {
        var templates = await templateRepository.GetByDoctorAsync(doctorId, ct);
        return templates.OrderByDescending(t => t.UseCount).Select(MapToDto).ToList();
    }

    public async Task<PrescriptionTemplateResponseDto?> GetByIdAsync(Guid templateId, Guid doctorId, CancellationToken ct = default)
    {
        var template = await templateRepository.GetByIdAsync(templateId, doctorId, ct);
        return template is null ? null : MapToDto(template);
    }

    public async Task<PrescriptionTemplateResponseDto> UpdateAsync(Guid templateId, Guid doctorId, UpdatePrescriptionTemplateDto dto, CancellationToken ct = default)
    {
        var template = await templateRepository.GetByIdAsync(templateId, doctorId, ct)
            ?? throw new NotFoundException(nameof(PrescriptionTemplate), templateId);

        template.Update(dto.Name, dto.Description, dto.Diagnosis, dto.Medications, dto.LabTests, dto.FollowUpInstructions);
        await templateRepository.UpdateAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToDto(template);
    }

    public async Task DeleteAsync(Guid templateId, Guid doctorId, CancellationToken ct = default)
    {
        var deleted = await templateRepository.DeleteAsync(templateId, doctorId, ct);
        if (!deleted) throw new NotFoundException(nameof(PrescriptionTemplate), templateId);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<PrescriptionResponseDto> CreateFromTemplateAsync(Guid doctorId, CreatePrescriptionFromTemplateDto dto, CancellationToken ct = default)
    {
        var template = await templateRepository.GetByIdAsync(dto.TemplateId, doctorId, ct)
            ?? throw new NotFoundException(nameof(PrescriptionTemplate), dto.TemplateId);

        var appointment = await appointmentRepository.GetByIdAsync(dto.AppointmentId)
            ?? throw new NotFoundException(nameof(Appointment), dto.AppointmentId);

        if (appointment.DoctorId != doctorId)
            throw new ForbiddenException("You can only issue prescriptions for your own appointments.");

        if (appointment.Status != AppointmentStatus.Completed && appointment.Status != AppointmentStatus.InProgress)
            throw new DomainException("Prescriptions can only be issued for in-progress or completed appointments.");

        var existing = await prescriptionRepository.GetByAppointmentAsync(dto.AppointmentId, ct);
        if (existing is not null)
            throw new DomainException("A prescription already exists for this appointment.");

        var medicationsJson = dto.MedicationsOverride ?? template.Medications;
        var medications = JsonSerializer.Deserialize<List<MedicationItem>>(medicationsJson)
            ?? throw new DomainException("Invalid medications format.");

        var labTests = dto.LabTestsOverride is not null
            ? JsonSerializer.Deserialize<List<string>>(dto.LabTestsOverride)
            : (template.LabTests is not null ? JsonSerializer.Deserialize<List<string>>(template.LabTests) : null);

        var prescription = Prescription.Issue(
            dto.AppointmentId,
            appointment.PatientId,
            doctorId,
            appointment.CenterId,
            dto.DiagnosisOverride ?? template.Diagnosis ?? string.Empty,
            medications,
            labTests,
            dto.FollowUpInstructionsOverride ?? template.FollowUpInstructions,
            dto.SpecialInstructions);

        var qrPayload = qrCodeService.BuildQrPayloadString(prescription.Id, prescription.IssueDate);
        prescription.SetQrCode(qrCodeService.GenerateQrCodeBase64(qrPayload));

        await prescriptionRepository.CreateAsync(prescription, ct);
        template.IncrementUseCount();
        await templateRepository.UpdateAsync(template, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new PrescriptionResponseDto(
            prescription.Id,
            prescription.AppointmentId,
            prescription.PatientId,
            prescription.DoctorId,
            prescription.CenterId,
            prescription.IssueDate,
            prescription.ExpiryDate,
            prescription.Status.ToString(),
            prescription.Diagnosis,
            prescription.Medications.Select(m => new MedicationDto(m.Name, m.Dosage, m.Frequency, m.Duration, m.Instructions)).ToList(),
            prescription.LabTests,
            prescription.FollowUpInstructions,
            prescription.SpecialInstructions,
            prescription.QrCode ?? string.Empty,
            prescription.PrescriptionUrl,
            prescription.ExpiryDate.HasValue && prescription.ExpiryDate.Value < DateOnly.FromDateTime(DateTime.UtcNow),
            string.Empty,
            string.Empty,
            string.Empty);
    }

    private static PrescriptionTemplateResponseDto MapToDto(PrescriptionTemplate t) =>
        new(t.Id, t.DoctorId, t.Name, t.Description, t.Diagnosis,
            t.Medications, t.LabTests, t.FollowUpInstructions,
            t.UseCount, t.CreatedAt, t.UpdatedAt);
}
