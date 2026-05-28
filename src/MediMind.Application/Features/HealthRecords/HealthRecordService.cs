using AutoMapper;
using FluentValidation;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;

namespace MediMind.Application.Features.HealthRecords;

public interface IHealthRecordService
{
    Task<HealthRecordResponseDto> CreateAsync(Guid patientId, CreateHealthRecordDto dto);
    Task<PaginatedHealthRecordsDto> GetByPatientAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate, int page, int pageSize);
    Task<HealthRecordResponseDto?> GetByIdAsync(Guid recordId, Guid requesterId, string requesterRole, Guid? requesterCenterId);
    Task<HealthRecordResponseDto?> UpdateAsync(Guid recordId, Guid patientId, UpdateHealthRecordDto dto);
    Task<bool> DeleteAsync(Guid recordId, Guid patientId);
    Task<HealthTrendsResponseDto> GetTrendAsync(Guid patientId, int days = 30);
    Task<HealthRecordResponseDto?> GetLatestAsync(Guid patientId);
    Task<int> GetCountAsync(Guid patientId);
    void ValidatePatientOwnership(Guid requesterPatientId, Guid targetPatientId);
}

public class HealthRecordService(
    IHealthRecordRepository repository,
    IAppointmentRepository appointmentRepository,
    IMapper mapper,
    IValidator<CreateHealthRecordDto> createValidator) : IHealthRecordService
{
    public async Task<HealthRecordResponseDto> CreateAsync(Guid patientId, CreateHealthRecordDto dto)
    {
        await createValidator.ValidateAndThrowAsync(dto);

        var record = HealthRecord.Create(
            patientId,
            dto.RecordDate,
            dto.RecordTime,
            dto.SystolicBp,
            dto.DiastolicBp,
            dto.GlucoseLevel,
            dto.Weight,
            dto.Height,
            dto.Temperature,
            dto.HeartRate,
            dto.OxygenSaturation,
            dto.RespiratoryRate,
            dto.Notes,
            NormalizeRecordedBy(dto.RecordedBy));

        var created = await repository.CreateAsync(record);
        return mapper.Map<HealthRecordResponseDto>(created);
    }

    public async Task<PaginatedHealthRecordsDto> GetByPatientAsync(Guid patientId, DateOnly? startDate, DateOnly? endDate, int page, int pageSize)
    {
        var records = await repository.GetByPatientIdAsync(patientId, startDate, endDate, page, pageSize);
        var total = await repository.GetRecordCountAsync(patientId);
        var totalPages = (int)Math.Ceiling(total / (double)pageSize);
        return new PaginatedHealthRecordsDto(
            mapper.Map<IEnumerable<HealthRecordSummaryDto>>(records),
            total,
            page,
            pageSize,
            totalPages);
    }

    public async Task<HealthRecordResponseDto?> GetByIdAsync(Guid recordId, Guid requesterId, string requesterRole, Guid? requesterCenterId)
    {
        var role = requesterRole.Trim();

        if (role == "Patient")
        {
            var own = await repository.GetByIdAsync(recordId, requesterId);
            return own is null ? null : mapper.Map<HealthRecordResponseDto>(own);
        }

        var targetRecord = await repository.GetByIdAsync(recordId);

        if (targetRecord is null)
            return null;

        if (role == "Doctor")
        {
            // Doctor can only read records for patients with confirmed appointments at doctor's center.
            var appointments = await appointmentRepository.GetByPatientAsync(targetRecord.PatientId);
            var hasAccess = appointments.Any(a =>
                a.DoctorId == requesterId &&
                a.Status == AppointmentStatus.Confirmed);

            if (!hasAccess)
                throw new UnauthorizedException();
        }
        else if (role == "Admin")
        {
            // Admin can read records for patients enrolled in their center (inferred from appointments at that center).
            if (!requesterCenterId.HasValue)
                throw new UnauthorizedException();

            var appointments = await appointmentRepository.GetByPatientAsync(targetRecord.PatientId);
            var hasAccess = appointments.Any(a => a.CenterId == requesterCenterId.Value);

            if (!hasAccess)
                throw new UnauthorizedException();
        }

        return mapper.Map<HealthRecordResponseDto>(targetRecord);
    }

    public async Task<HealthRecordResponseDto?> UpdateAsync(Guid recordId, Guid patientId, UpdateHealthRecordDto dto)
    {
        var record = await repository.GetByIdAsync(recordId, patientId);
        if (record is null)
            return null;

        var createEquivalent = mapper.Map<CreateHealthRecordDto>(dto);
        await createValidator.ValidateAndThrowAsync(createEquivalent);

        record.UpdateVitals(
            dto.RecordDate,
            dto.RecordTime,
            dto.SystolicBp,
            dto.DiastolicBp,
            dto.GlucoseLevel,
            dto.Weight,
            dto.Height,
            dto.Temperature,
            dto.HeartRate,
            dto.OxygenSaturation,
            dto.RespiratoryRate,
            dto.Notes,
            NormalizeRecordedBy(dto.RecordedBy));

        var updated = await repository.UpdateAsync(record);
        return updated is null ? null : mapper.Map<HealthRecordResponseDto>(updated);
    }

    public async Task<bool> DeleteAsync(Guid recordId, Guid patientId) =>
        await repository.DeleteAsync(recordId, patientId);

    public async Task<HealthTrendsResponseDto> GetTrendAsync(Guid patientId, int days = 30)
    {
        var allowedDays = new[] { 7, 14, 30, 90 };
        var normalizedDays = allowedDays.Contains(days) ? days : 30;
        return await repository.GetTrendAsync(patientId, normalizedDays);
    }

    public async Task<HealthRecordResponseDto?> GetLatestAsync(Guid patientId)
    {
        var latest = await repository.GetLatestAsync(patientId);
        return latest is null ? null : mapper.Map<HealthRecordResponseDto>(latest);
    }

    public Task<int> GetCountAsync(Guid patientId) => repository.GetRecordCountAsync(patientId);

    public void ValidatePatientOwnership(Guid requesterPatientId, Guid targetPatientId)
    {
        if (requesterPatientId != targetPatientId)
            throw new UnauthorizedException();
    }

    private static string NormalizeRecordedBy(string? value)
    {
        var lower = value?.Trim().ToLowerInvariant();
        return lower is "patient" or "doctor" ? lower : "patient";
    }
}
