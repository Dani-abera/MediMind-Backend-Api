using MediMind.API.Attributes;
using MediMind.Application.Features.Appointments;
using MediMind.Application.Features.CenterManagement;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Doctor profile and availability endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/doctors")]
[Tags("Doctors")]
public class DoctorsController(
    IDoctorRepository doctorRepository,
    IAppointmentAvailabilityService availabilityService) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DoctorResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Search([FromQuery] Guid? centerId, [FromQuery] string? specialization, [FromQuery] string? name, [FromQuery] DateOnly? availableOnDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
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

        return Ok(new PagedResult<DoctorResponseDto>(doctors, result.Page, result.PageSize, result.TotalCount));
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(DoctorResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get(Guid id)
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

    [HttpGet("{id:guid}/availability")]
    [RequireRole("Patient")]
    public async Task<IActionResult> Availability(Guid id, [FromQuery] Guid centerId, [FromQuery] DateOnly date)
    {
        var slots = await availabilityService.GetAvailableSlotsAsync(id, centerId, date);
        return Ok(slots.Select(s => new { time = s.Time, s.IsAvailable, s.SlotDuration }));
    }

    [HttpGet("{id:guid}/available-dates")]
    [RequireRole("Patient")]
    public async Task<IActionResult> AvailableDates(Guid id, [FromQuery] Guid centerId, [FromQuery] int daysAhead = 30)
    {
        var dates = await availabilityService.GetAvailableDatesAsync(id, centerId, daysAhead);
        return Ok(dates);
    }
}
