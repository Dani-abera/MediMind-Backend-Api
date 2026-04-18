using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.Appointments;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Appointment management endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/appointments")]
[Tags("Appointments")]
public class AppointmentsController(
    IAppointmentService appointmentService,
    IAppointmentAvailabilityService availabilityService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Books a new appointment.
    /// </summary>
    [HttpPost]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Book([FromBody] CreateAppointmentDto dto)
    {
        var response = await appointmentService.BookAppointmentAsync(dto, currentUser.UserId);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Gets scoped appointments for current user role.
    /// </summary>
    [HttpGet]
    [RequireRole("Patient", "Doctor", "Admin")]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] AppointmentStatus? status, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var filter = new AppointmentFilterDto(status, startDate, endDate, null, page, pageSize);
        var result = await appointmentService.GetAppointmentsAsync(currentUser.UserId, currentUser.UserType, currentUser.TenantId, filter);
        return Ok(result);
    }

    /// <summary>
    /// Gets appointment details.
    /// </summary>
    [HttpGet("{id:guid}")]
    [RequireRole("Patient", "Doctor", "Admin")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id)
    {
        var result = await appointmentService.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, currentUser.TenantId);
        if (result is null)
            return NotFound(new { error = "Appointment not found" });
        return Ok(result);
    }

    /// <summary>
    /// Cancels appointment by patient/admin.
    /// </summary>
    [HttpPost("{id:guid}/cancel")]
    [RequireRole("Patient", "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelAppointmentDto dto)
    {
        await appointmentService.CancelAppointmentAsync(id, currentUser.UserId, dto, currentUser.UserType, currentUser.TenantId);
        return NoContent();
    }

    /// <summary>
    /// Reschedules patient appointment.
    /// </summary>
    [HttpPost("{id:guid}/reschedule")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentDto dto)
    {
        var response = await appointmentService.RescheduleAppointmentAsync(id, currentUser.UserId, dto);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>
    /// Approves pending appointment.
    /// </summary>
    [HttpPost("{id:guid}/approve")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id)
    {
        var centerId = currentUser.TenantId ?? throw new UnauthorizedException();
        var response = await appointmentService.ApproveAppointmentAsync(id, currentUser.UserId, centerId);
        return Ok(response);
    }

    /// <summary>
    /// Rejects pending appointment.
    /// </summary>
    [HttpPost("{id:guid}/reject")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApproveRejectDto dto)
    {
        var centerId = currentUser.TenantId ?? throw new UnauthorizedException();
        await appointmentService.RejectAppointmentAsync(id, currentUser.UserId, centerId, dto.Reason ?? "Rejected by admin");
        return NoContent();
    }

    /// <summary>
    /// Returns available slots for doctor/date.
    /// </summary>
    [HttpGet("availability")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AvailabilityResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Availability([FromQuery] Guid doctorId, [FromQuery] Guid centerId, [FromQuery] DateOnly date)
    {
        var slots = await availabilityService.GetAvailableSlotsAsync(doctorId, centerId, date);
        var nextDate = (await availabilityService.GetAvailableDatesAsync(doctorId, centerId, 30)).FirstOrDefault(d => d >= date);

        var response = new AvailabilityResponseDto(
            doctorId,
            centerId,
            date,
            slots.Select(s => new AvailabilitySlotDto(s.Time.ToString("HH:mm"), s.IsAvailable)).ToList(),
            nextDate == default ? null : nextDate);

        return Ok(response);
    }

    /// <summary>
    /// Returns available dates for doctor/center.
    /// </summary>
    [HttpGet("available-dates")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(List<DateOnly>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableDates([FromQuery] Guid doctorId, [FromQuery] Guid centerId, [FromQuery] int daysAhead = 30)
    {
        var dates = await availabilityService.GetAvailableDatesAsync(doctorId, centerId, daysAhead);
        return Ok(dates);
    }
}
