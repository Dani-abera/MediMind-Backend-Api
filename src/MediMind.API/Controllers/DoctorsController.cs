using MediMind.API.Attributes;
using MediMind.Application.Features.Appointments;
using MediMind.Application.Features.CenterManagement;
using MediMind.Application.Features.Doctors;
using MediMind.Application.Features.HealthPredictions;
using MediMind.Application.Features.HealthRecords;
using MediMind.Application.Features.MedicalHistory;
using MediMind.Application.Features.Prescriptions;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Doctor directory search, availability, and doctor-facing dashboard.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/doctors")]
public class DoctorsController(
    IDoctorRepository doctorRepository,
    IAppointmentAvailabilityService availabilityService,
    IDoctorProfileService doctorProfileService,
    IDoctorPatientAccessChecker patientAccessChecker,
    IHealthRecordService healthRecordService,
    IHealthPredictionService healthPredictionService,
    IMedicalHistoryService medicalHistoryService,
    IPrescriptionService prescriptionService,
    IDoctorScheduleRepository scheduleRepository,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Search the doctor directory with optional filters (FR-010).</summary>
    [Tags("Doctor — Profile")]
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<DoctorResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] Guid? centerId, [FromQuery] string? specialization, [FromQuery] string? name, [FromQuery] DateOnly? availableOnDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var search = new DoctorSearchDto(centerId, specialization, name, availableOnDate, page, pageSize);
        var result = await doctorRepository.SearchAsync(search);

        var doctors = new List<DoctorResponseDto>();
        foreach (var doctor in result.Items)
        {
            DateTime? nextSlot = null;
            if (centerId.HasValue)
            {
                var start = DateOnly.FromDateTime(DateTime.UtcNow);
                for (var i = 0; i < 14 && nextSlot is null; i++)
                {
                    var date = start.AddDays(i);
                    var slots = await availabilityService.GetAvailableSlotsAsync(doctor.Id, centerId.Value, date);
                    var first = slots.FirstOrDefault(s => s.IsAvailable);
                    if (first != default)
                        nextSlot = date.ToDateTime(first.Time);
                }
            }

            doctors.Add(new DoctorResponseDto(
                doctor.Id,
                doctor.FullName,
                doctor.Specialization,
                doctor.LicenseNumber,
                doctor.YearsOfExperience,
                doctor.Qualifications,
                doctor.LanguagesSpoken,
                doctor.DoctorHealthcareCenters.FirstOrDefault(x => centerId.HasValue && x.CenterId == centerId.Value)?.ConsultationFee,
                nextSlot,
                doctor.DoctorHealthcareCenters.Where(x => x.IsActive).Select(x => new DoctorCenterInfoDto(x.CenterId, x.Center.CenterName, x.ConsultationFee)).ToList()));
        }

        string? emptyMessage = null;
        if (doctors.Count == 0 && !string.IsNullOrEmpty(specialization) && centerId.HasValue)
            emptyMessage = $"No {specialization} doctors at this center";

        return Ok(new
        {
            items = doctors,
            totalCount = result.TotalCount,
            page = result.Page,
            pageSize = result.PageSize,
            message = emptyMessage
        });
    }

    /// <summary>Get a doctor's full profile by ID (FR-010).</summary>
    [Tags("Doctor — Profile")]
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(DoctorResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var doctor = await doctorRepository.GetByIdAsync(id);
        if (doctor is null)
            return NotFound(new { error = "Doctor not found" });

        var dto = new DoctorResponseDto(
            doctor.Id,
            doctor.FullName,
            doctor.Specialization,
            doctor.LicenseNumber,
            doctor.YearsOfExperience,
            doctor.Qualifications,
            doctor.LanguagesSpoken,
            null,
            null,
            doctor.DoctorHealthcareCenters.Where(x => x.IsActive).Select(x => new DoctorCenterInfoDto(x.CenterId, x.Center.CenterName, x.ConsultationFee)).ToList());

        return Ok(dto);
    }

    /// <summary>Get available time slots for a specific doctor on a given date.</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("{id:guid}/availability")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Availability(Guid id, [FromQuery] Guid centerId, [FromQuery] DateOnly date)
    {
        var slots = await availabilityService.GetAvailableSlotsAsync(id, centerId, date);
        return Ok(slots.Select(s => new { time = s.Time, s.IsAvailable, s.SlotDuration }));
    }

    /// <summary>Get dates on which a specific doctor has at least one available slot.</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("{id:guid}/available-dates")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableDates(Guid id, [FromQuery] Guid centerId, [FromQuery] int daysAhead = 30)
    {
        var dates = await availabilityService.GetAvailableDatesAsync(id, centerId, daysAhead);
        return Ok(dates);
    }

    // ─── Doctor-facing /me endpoints ──────────────────────────────────────────

    /// <summary>Get the authenticated doctor's own profile (FR-020).</summary>
    [Tags("Doctor — Profile")]
    [HttpGet("me")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(DoctorProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMyProfile(CancellationToken ct)
    {
        var profile = await doctorProfileService.GetProfileAsync(currentUser.UserId);
        return profile is null ? NotFound() : Ok(profile);
    }

    /// <summary>Update the authenticated doctor's editable profile fields (FR-020).</summary>
    [Tags("Doctor — Profile")]
    [HttpPut("me")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(DoctorProfileDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateMyProfile([FromBody] UpdateDoctorProfileDto dto, CancellationToken ct)
    {
        var updated = await doctorProfileService.UpdateProfileAsync(currentUser.UserId, dto);
        return Ok(updated);
    }

    /// <summary>Get all healthcare centers the authenticated doctor is affiliated with.</summary>
    [Tags("Doctor — Profile")]
    [HttpGet("me/centers")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(List<DoctorAffiliatedCenterDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyCenters(CancellationToken ct)
    {
        var centers = await doctorProfileService.GetCentersAsync(currentUser.UserId);
        return Ok(centers);
    }

    /// <summary>Get all weekly schedules for the authenticated doctor across all centers (FR-020).</summary>
    [Tags("Doctor — Profile")]
    [HttpGet("me/schedules")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMySchedules(CancellationToken ct)
    {
        var rows = await scheduleRepository.GetAllByDoctorAsync(currentUser.UserId);
        var result = rows.Select(r =>
        {
            var s = r.Schedule;
            var breaks = new List<object>();
            if (s.BreakStart.HasValue && s.BreakEnd.HasValue)
            {
                breaks.Add(new
                {
                    label = "Break",
                    startMinuteOfDay = s.BreakStart.Value.Hour * 60 + s.BreakStart.Value.Minute,
                    durationMinutes = (int)(s.BreakEnd.Value - s.BreakStart.Value).TotalMinutes
                });
            }

            return new
            {
                doctorId = s.DoctorId,
                centerId = s.CenterId,
                centerName = r.CenterName,
                workingDays = s.WorkingDays,
                startHour = s.StartTime.Hour,
                startMinute = s.StartTime.Minute,
                endHour = s.EndTime.Hour,
                endMinute = s.EndTime.Minute,
                slotDurationMinutes = s.SlotDuration,
                breaks
            };
        });
        return Ok(new { data = result });
    }

    /// <summary>Get today's appointments for the authenticated doctor (FR-021).</summary>
    [Tags("Doctor — Dashboard")]
    [HttpGet("me/today")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(List<DoctorTodayAppointmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTodayDashboard(CancellationToken ct)
    {
        var appointments = await doctorProfileService.GetTodayAppointmentsAsync(currentUser.UserId);
        return Ok(appointments);
    }

    /// <summary>Get the current queue at a specific center (my assigned patients only) (FR-021).</summary>
    [Tags("Doctor — Dashboard")]
    [HttpGet("me/queue/{centerId:guid}")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(List<DoctorQueueItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyQueue(Guid centerId, CancellationToken ct)
    {
        var queue = await doctorProfileService.GetQueueAsync(currentUser.UserId, centerId);
        return Ok(queue);
    }

    /// <summary>Get all distinct patients the doctor has seen, with optional search.</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("me/patients")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(DoctorPatientsPageDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPatients([FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var result = await doctorProfileService.GetPatientsAsync(currentUser.UserId, search, page, pageSize);
        return Ok(result);
    }

    /// <summary>Get a specific patient's profile (doctor must have an active/completed appointment) (FR-023).</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("me/patients/{patientId:guid}")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(DoctorPatientProfileDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientProfile(Guid patientId, CancellationToken ct)
    {
        await patientAccessChecker.EnsureAccessAsync(currentUser.UserId, patientId, ct);
        var profile = await doctorProfileService.GetPatientProfileAsync(currentUser.UserId, patientId, ct);
        return profile is null ? NotFound() : Ok(profile);
    }

    // ─── Patient data access (FR-023) ─────────────────────────────────────────

    /// <summary>Get a patient's health records (doctor must have active/completed appointment) (FR-023).</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("/api/v1/patients/{patientId:guid}/health-records")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(PaginatedHealthRecordsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPatientHealthRecords(Guid patientId, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        await patientAccessChecker.EnsureAccessAsync(currentUser.UserId, patientId);
        var records = await healthRecordService.GetByPatientAsync(patientId, startDate, endDate, page, pageSize);
        return Ok(records);
    }

    /// <summary>Get a patient's latest AI health prediction (FR-023).</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("/api/v1/patients/{patientId:guid}/predictions/latest")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(HealthPredictionResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientLatestPrediction(Guid patientId)
    {
        await patientAccessChecker.EnsureAccessAsync(currentUser.UserId, patientId);
        var prediction = await healthPredictionService.GetLatestAsync(patientId);
        return prediction is null ? NotFound() : Ok(prediction);
    }

    /// <summary>Get a patient's medical history (FR-023).</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("/api/v1/patients/{patientId:guid}/medical-history")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(MedicalHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPatientMedicalHistory(Guid patientId)
    {
        await patientAccessChecker.EnsureAccessAsync(currentUser.UserId, patientId);
        var history = await medicalHistoryService.GetAsync(patientId);
        return history is null ? NotFound() : Ok(history);
    }

    /// <summary>Get all prescriptions for a patient (read-only for doctor, FR-023).</summary>
    [Tags("Doctor — Patients")]
    [HttpGet("/api/v1/patients/{patientId:guid}/prescriptions")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<PrescriptionResponseDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPatientPrescriptions(Guid patientId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        await patientAccessChecker.EnsureAccessAsync(currentUser.UserId, patientId);
        var prescriptions = await prescriptionService.ListForRequesterAsync(patientId, "Patient", page, pageSize);
        return Ok(prescriptions);
    }
}
