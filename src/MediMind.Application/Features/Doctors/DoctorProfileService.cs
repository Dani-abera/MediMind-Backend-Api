using System.Text.Json;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.HealthRecords;
using MediMind.Application.Features.Prescriptions;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using AutoMapper;

namespace MediMind.Application.Features.Doctors;

public interface IDoctorProfileService
{
    Task<DoctorProfileDto?> GetProfileAsync(Guid doctorId, CancellationToken ct = default);
    Task<DoctorProfileDto> UpdateProfileAsync(Guid doctorId, UpdateDoctorProfileDto dto, CancellationToken ct = default);
    Task<List<DoctorAffiliatedCenterDto>> GetCentersAsync(Guid doctorId, CancellationToken ct = default);
    Task<List<DoctorTodayAppointmentDto>> GetTodayAppointmentsAsync(Guid doctorId, CancellationToken ct = default);
    Task<List<DoctorQueueItemDto>> GetQueueAsync(Guid doctorId, Guid centerId, CancellationToken ct = default);
    Task<DoctorPatientsPageDto> GetPatientsAsync(Guid doctorId, string? search, int page, int pageSize, CancellationToken ct = default);
    Task<DoctorPatientProfileDto?> GetPatientProfileAsync(Guid doctorId, Guid patientId, CancellationToken ct = default);
}

public interface IDoctorPatientAccessChecker
{
    Task<bool> HasAccessAsync(Guid doctorId, Guid patientId, CancellationToken ct = default);
    Task EnsureAccessAsync(Guid doctorId, Guid patientId, CancellationToken ct = default);
}

public class DoctorProfileService(
    IDoctorRepository doctorRepository,
    IAppointmentRepository appointmentRepository,
    IQueueRepository queueRepository,
    IPrescriptionRepository prescriptionRepository,
    IPatientRepository patientRepository,
    IPatientMedicalHistoryRepository medicalHistoryRepository,
    IUnitOfWork unitOfWork) : IDoctorProfileService
{
    public async Task<DoctorProfileDto?> GetProfileAsync(Guid doctorId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId);
        if (doctor is null) return null;
        return MapToProfileDto(doctor);
    }

    public async Task<DoctorProfileDto> UpdateProfileAsync(Guid doctorId, UpdateDoctorProfileDto dto, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        doctor.UpdateProfile(dto.Biography, dto.LanguagesSpoken, dto.Qualifications, dto.ProfileImageUrl);
        await unitOfWork.SaveChangesAsync(ct);
        return MapToProfileDto(doctor);
    }

    public async Task<List<DoctorAffiliatedCenterDto>> GetCentersAsync(Guid doctorId, CancellationToken ct = default)
    {
        var doctor = await doctorRepository.GetByIdAsync(doctorId)
            ?? throw new NotFoundException(nameof(Doctor), doctorId);

        return doctor.DoctorHealthcareCenters
            .Where(x => x.IsActive)
            .Select(x => new DoctorAffiliatedCenterDto(x.CenterId, x.Center.CenterName, x.ConsultationFee, x.JoinedDate, x.IsActive))
            .ToList();
    }

    public async Task<List<DoctorTodayAppointmentDto>> GetTodayAppointmentsAsync(Guid doctorId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var appointments = await appointmentRepository.GetByDoctorAndDateAsync(doctorId, today, ct);

        var result = new List<DoctorTodayAppointmentDto>();
        foreach (var appt in appointments.OrderBy(a => a.AppointmentTime))
        {
            var patientAppointments = await appointmentRepository.GetByPatientAsync(appt.PatientId, ct);
            var lastVisit = patientAppointments
                .Where(a => a.Status == AppointmentStatus.Completed && a.AppointmentDate < today)
                .OrderByDescending(a => a.AppointmentDate)
                .Select(a => (DateOnly?)a.AppointmentDate)
                .FirstOrDefault();

            var prescriptionCount = (await prescriptionRepository.GetByPatientAsync(appt.PatientId, 1, int.MaxValue, ct)).Count;

            var queue = await queueRepository.GetByAppointmentIdAsync(appt.Id);

            result.Add(new DoctorTodayAppointmentDto(
                appt.Id,
                appt.Status.ToString(),
                appt.AppointmentDate,
                appt.AppointmentTime,
                appt.ReasonForVisit,
                queue?.QueueNumber,
                queue?.EstimatedWaitTimeMinutes,
                new DoctorPatientSummaryDto(
                    appt.PatientId,
                    appt.Patient?.FullName ?? string.Empty,
                    appt.Patient?.PhoneNumber ?? string.Empty,
                    lastVisit,
                    patientAppointments.Count(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Completed)),
                prescriptionCount));
        }
        return result;
    }

    public async Task<List<DoctorQueueItemDto>> GetQueueAsync(Guid doctorId, Guid centerId, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var queueEntries = await queueRepository.GetByCenterAndDateAsync(centerId, today, ct);

        var result = new List<DoctorQueueItemDto>();
        foreach (var entry in queueEntries.OrderBy(q => q.Position))
        {
            var appt = entry.Appointment;
            if (appt is null || appt.DoctorId != doctorId) continue;
            result.Add(new DoctorQueueItemDto(
                entry.QueueNumber,
                entry.Position,
                entry.EstimatedWaitTimeMinutes,
                entry.Status.ToString(),
                appt.PatientId,
                appt.Patient?.FullName ?? string.Empty,
                appt.AppointmentTime));
        }
        return result;
    }

    public async Task<DoctorPatientsPageDto> GetPatientsAsync(Guid doctorId, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var filter = new AppointmentFilterDto(null, null, null, doctorId, 1, int.MaxValue);
        var allAppointments = await appointmentRepository.GetByDoctorAsync(doctorId, Guid.Empty, filter);

        var patientGroups = allAppointments.Items
            .GroupBy(a => a.PatientId)
            .Select(g =>
            {
                var latest = g.OrderByDescending(a => a.AppointmentDate).First();
                return new DoctorPatientSummaryDto(
                    g.Key,
                    latest.Patient?.FullName ?? string.Empty,
                    latest.Patient?.PhoneNumber ?? string.Empty,
                    g.Where(a => a.Status == AppointmentStatus.Completed)
                        .OrderByDescending(a => a.AppointmentDate)
                        .Select(a => (DateOnly?)a.AppointmentDate)
                        .FirstOrDefault(),
                    g.Count(a => a.Status == AppointmentStatus.Completed));
            })
            .AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
            patientGroups = patientGroups.Where(p =>
                p.FullName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                p.PhoneNumber.Contains(search));

        var ordered = patientGroups.OrderByDescending(p => p.LastVisit).ToList();
        var total = ordered.Count;
        var boundedPage = Math.Max(page, 1);
        var boundedSize = Math.Clamp(pageSize, 1, 100);
        var paged = ordered.Skip((boundedPage - 1) * boundedSize).Take(boundedSize).ToList();

        return new DoctorPatientsPageDto(paged, boundedPage, boundedSize, total);
    }

    public async Task<DoctorPatientProfileDto?> GetPatientProfileAsync(Guid doctorId, Guid patientId, CancellationToken ct = default)
    {
        var patient = await patientRepository.GetByUserIdAsync(patientId, ct);
        if (patient is null) return null;

        var appointments = await appointmentRepository.GetByPatientAsync(patientId, ct);
        var history = await medicalHistoryRepository.GetByPatientIdAsync(patientId, ct);

        var lastVisit = appointments
            .Where(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Completed)
            .OrderByDescending(a => a.AppointmentDate)
            .Select(a => (DateOnly?)a.AppointmentDate)
            .FirstOrDefault();
        var totalVisits = appointments.Count(a => a.DoctorId == doctorId && a.Status == AppointmentStatus.Completed);

        var primaryContact = patient.EmergencyContacts.FirstOrDefault(c => c.IsPrimary)
                             ?? patient.EmergencyContacts.FirstOrDefault();

        // Prefer structured medical history (written by mobile app) over legacy Patient fields
        var bloodType = history?.BloodType ?? patient.BloodType?.ToString();
        var chronicConditions = ParseJsonStringArray(history?.ChronicConditions)
                                ?? patient.ChronicConditions;
        var allergies = ParseAllergenNames(history?.Allergies)
                        ?? patient.Allergies?.Split(',').Select(a => a.Trim())
                            .Where(a => !string.IsNullOrEmpty(a)).ToList()
                        ?? [];
        var currentMedications = ParseMedicationNames(history?.CurrentMedications)
                                 ?? patient.CurrentMedications;

        return new DoctorPatientProfileDto(
            patient.Id.ToString(),
            patient.FullName,
            patient.DateOfBirth,
            patient.Age,
            patient.Gender.ToString(),
            patient.PhoneNumber,
            patient.Email,
            bloodType,
            patient.Address,
            primaryContact is null ? null : new DoctorEmergencyContactDto(
                primaryContact.FullName,
                primaryContact.PhoneNumber,
                primaryContact.Relationship.ToString()),
            chronicConditions,
            allergies,
            currentMedications,
            patient.ProfileImageUrl,
            lastVisit,
            totalVisits);
    }

    private static List<string>? ParseJsonStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;
        try
        {
            var items = JsonSerializer.Deserialize<List<string>>(json);
            return items is { Count: > 0 } ? items : null;
        }
        catch { return null; }
    }

    private static List<string>? ParseAllergenNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;
        try
        {
            // Stored as [{"allergen":"...","severity":"..."}] — extract allergen names
            var items = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (items is null or { Count: 0 }) return null;
            var names = items
                .Select(e => e.TryGetProperty("allergen", out var v) ? v.GetString() : null)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToList();
            return names.Count > 0 ? names : null;
        }
        catch { return null; }
    }

    private static List<string>? ParseMedicationNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]") return null;
        try
        {
            // Try plain string array first
            var asStrings = JsonSerializer.Deserialize<List<string>>(json);
            if (asStrings is { Count: > 0 }) return asStrings;
            // Try object array — extract "name" field
            var items = JsonSerializer.Deserialize<List<JsonElement>>(json);
            if (items is null or { Count: 0 }) return null;
            var names = items
                .Select(e => e.TryGetProperty("name", out var v) ? v.GetString() : null)
                .Where(s => !string.IsNullOrEmpty(s))
                .Select(s => s!)
                .ToList();
            return names.Count > 0 ? names : null;
        }
        catch { return null; }
    }

    private static DoctorProfileDto MapToProfileDto(Doctor doctor) =>
        new(
            doctor.Id,
            doctor.Email,
            doctor.FullName,
            doctor.BadgeNumber,
            doctor.Specialization,
            doctor.LicenseNumber,
            doctor.LicenseVerified,
            doctor.YearsOfExperience,
            doctor.Qualifications?.Split(',').Select(q => q.Trim()).Where(q => !string.IsNullOrEmpty(q)).ToList() ?? [],
            doctor.LanguagesSpoken,
            doctor.ProfileImageUrl,
            doctor.Biography,
            doctor.DoctorHealthcareCenters
                .Where(x => x.IsActive)
                .Select(x => new DoctorAffiliatedCenterDto(x.CenterId, x.Center.CenterName, x.ConsultationFee, x.JoinedDate, x.IsActive))
                .ToList());
}

public class DoctorPatientAccessChecker(IAppointmentRepository appointmentRepository) : IDoctorPatientAccessChecker
{
    private static readonly AppointmentStatus[] ValidStatuses =
        [AppointmentStatus.Pending, AppointmentStatus.Confirmed, AppointmentStatus.InProgress, AppointmentStatus.Completed];

    public async Task<bool> HasAccessAsync(Guid doctorId, Guid patientId, CancellationToken ct = default)
    {
        var appointments = await appointmentRepository.GetByPatientAsync(patientId, ct);
        return appointments.Any(a => a.DoctorId == doctorId && ValidStatuses.Contains(a.Status));
    }

    public async Task EnsureAccessAsync(Guid doctorId, Guid patientId, CancellationToken ct = default)
    {
        if (!await HasAccessAsync(doctorId, patientId, ct))
            throw new ForbiddenException("No active or completed appointment found for this patient.");
    }
}
