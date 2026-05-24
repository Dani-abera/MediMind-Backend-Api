using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.Appointments;
using MediMind.Application.Features.AppointmentNotes;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

/// <summary>
/// Appointment booking, status management, and availability queries.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/appointments")]
public class AppointmentsController(
    IAppointmentService appointmentService,
    IAppointmentAvailabilityService availabilityService,
    IAppointmentNoteService appointmentNoteService,
    IWaitlistService waitlistService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Book a new appointment for the authenticated patient (FR-011).</summary>
    [Tags("Patient — Appointments")]
    [HttpPost]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Book([FromBody] CreateAppointmentDto dto, CancellationToken ct)
    {
        var response = await appointmentService.BookAppointmentAsync(dto, currentUser.UserId);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>List appointments scoped to the caller's role (patient / doctor / admin) (FR-012).</summary>
    [Tags("Patient — Appointments", "Doctor — Appointments", "Admin — Appointments")]
    [HttpGet]
    [RequireRole("Patient", "Doctor", "Admin")]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Get([FromQuery] AppointmentStatus? status, [FromQuery] DateOnly? startDate, [FromQuery] DateOnly? endDate, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        var filter = new AppointmentFilterDto(status, startDate, endDate, null, page, pageSize);
        var result = await appointmentService.GetAppointmentsAsync(currentUser.UserId, currentUser.UserType, currentUser.TenantId, filter);
        return Ok(result);
    }

    /// <summary>Get appointment details by ID (FR-012).</summary>
    [Tags("Patient — Appointments", "Doctor — Appointments", "Admin — Appointments")]
    [HttpGet("{id:guid}")]
    [RequireRole("Patient", "Doctor", "Admin")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var result = await appointmentService.GetByIdAsync(id, currentUser.UserId, currentUser.UserType, currentUser.TenantId);
        if (result is null)
            return NotFound(new { error = "Appointment not found" });
        return Ok(result);
    }

    /// <summary>Cancel an appointment by patient, doctor, or admin (FR-013).</summary>
    [Tags("Patient — Appointments", "Doctor — Appointments", "Admin — Appointments")]
    [HttpPost("{id:guid}/cancel")]
    [RequireRole("Patient", "Doctor", "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Cancel(Guid id, [FromBody] CancelAppointmentDto dto, CancellationToken ct)
    {
        await appointmentService.CancelAppointmentAsync(id, currentUser.UserId, dto, currentUser.UserType, currentUser.TenantId);
        return NoContent();
    }

    /// <summary>Reschedule an existing patient appointment (FR-014).</summary>
    [Tags("Patient — Appointments")]
    [HttpPost("{id:guid}/reschedule")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> Reschedule(Guid id, [FromBody] RescheduleAppointmentDto dto, CancellationToken ct)
    {
        var response = await appointmentService.RescheduleAppointmentAsync(id, currentUser.UserId, dto);
        return StatusCode(StatusCodes.Status201Created, response);
    }

    /// <summary>Approve a pending appointment request (FR-015).</summary>
    [Tags("Admin — Appointments")]
    [HttpPost("{id:guid}/approve")]
    [RequireRole("Admin")]
    [ProducesResponseType(typeof(AppointmentResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        var centerId = currentUser.TenantId ?? throw new UnauthorizedException();
        var response = await appointmentService.ApproveAppointmentAsync(id, currentUser.UserId, centerId);
        return Ok(response);
    }

    /// <summary>Reject a pending appointment request with a reason (FR-015).</summary>
    [Tags("Admin — Appointments")]
    [HttpPost("{id:guid}/reject")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ApproveRejectDto dto, CancellationToken ct)
    {
        var centerId = currentUser.TenantId ?? throw new UnauthorizedException();
        await appointmentService.RejectAppointmentAsync(id, currentUser.UserId, centerId, dto.Reason ?? "Rejected by admin");
        return NoContent();
    }

    /// <summary>Doctor declines a pending appointment with a reason.</summary>
    [Tags("Doctor — Appointments")]
    [HttpPost("{id:guid}/doctor-reject")]
    [RequireRole("Doctor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DoctorReject(Guid id, [FromBody] ApproveRejectDto dto, CancellationToken ct)
    {
        await appointmentService.DoctorRejectAppointmentAsync(id, currentUser.UserId, dto.Reason ?? "Declined by doctor");
        return NoContent();
    }

    /// <summary>Upcoming appointments (date ≥ today, status Pending or Confirmed), sorted ascending (FR-012).</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("upcoming")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(IReadOnlyList<AppointmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Upcoming(CancellationToken ct) =>
        Ok(await appointmentService.GetUpcomingAsync(currentUser.UserId, ct));

    /// <summary>Past appointments (date &lt; today or status Completed/Cancelled/NoShow), paginated descending (FR-012).</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("past")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(PagedResult<AppointmentResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Past([FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        Ok(await appointmentService.GetPastAsync(currentUser.UserId, page, pageSize, ct));

    /// <summary>Get available time slots for a doctor on a given date (FR-011).</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("availability")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(AvailabilityResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Availability([FromQuery] Guid doctorId, [FromQuery] Guid centerId, [FromQuery] DateOnly date, CancellationToken ct)
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

    /// <summary>Get dates on which a doctor has at least one available slot (FR-011).</summary>
    [Tags("Patient — Appointments")]
    [HttpGet("available-dates")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(List<DateOnly>), StatusCodes.Status200OK)]
    public async Task<IActionResult> AvailableDates([FromQuery] Guid doctorId, [FromQuery] Guid centerId, [FromQuery] int daysAhead = 30, CancellationToken ct = default)
    {
        var dates = await availabilityService.GetAvailableDatesAsync(doctorId, centerId, daysAhead);
        return Ok(dates);
    }

    // ─── Waitlist ─────────────────────────────────────────────────────────────

    /// <summary>Subscribe to be notified when a slot opens for a fully-booked doctor (FR-011).</summary>
    [Tags("Patient — Appointments")]
    [HttpPost("waitlist")]
    [RequireRole("Patient")]
    [ProducesResponseType(typeof(WaitlistResponseDto), StatusCodes.Status201Created)]
    public async Task<IActionResult> SubscribeWaitlist([FromBody] WaitlistSubscribeDto dto, CancellationToken ct)
    {
        var result = await waitlistService.SubscribeAsync(currentUser.UserId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Unsubscribe from a waitlist (FR-011).</summary>
    [Tags("Patient — Appointments")]
    [HttpDelete("waitlist/{id:guid}")]
    [RequireRole("Patient")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> UnsubscribeWaitlist(Guid id, CancellationToken ct)
    {
        await waitlistService.UnsubscribeAsync(id, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Doctor marks an in-progress or confirmed appointment as completed.</summary>
    [Tags("Doctor — Appointments")]
    [HttpPost("{id:guid}/complete")]
    [RequireRole("Doctor")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkComplete(Guid id, CancellationToken ct)
    {
        await appointmentService.MarkCompleteAsync(id, currentUser.UserId, ct);
        return NoContent();
    }

    /// <summary>Bulk-approve a list of pending appointments (admin shortcut).</summary>
    [Tags("Admin — Appointments")]
    [HttpPost("bulk-approve")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> BulkApprove([FromBody] BulkApproveDto dto, CancellationToken ct)
    {
        var centerId = currentUser.TenantId ?? throw new UnauthorizedException();
        foreach (var id in dto.Ids)
            await appointmentService.ApproveAppointmentAsync(id, currentUser.UserId, centerId);
        return NoContent();
    }

    // ─── Appointment Notes (private doctor notes) ──────────────────────────────

    /// <summary>Get the private doctor note for an appointment. Returns null if no note exists yet.</summary>
    [Tags("Doctor — Appointments")]
    [HttpGet("{id:guid}/notes")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(AppointmentNoteResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNote(Guid id, CancellationToken ct)
    {
        var note = await appointmentNoteService.GetAsync(id, currentUser.UserId, ct);
        return Ok(note);
    }

    /// <summary>Create or update the private doctor note for an appointment (upsert).</summary>
    [Tags("Doctor — Appointments")]
    [HttpPost("{id:guid}/notes")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(AppointmentNoteResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpsertNote(Guid id, [FromBody] UpsertAppointmentNoteDto dto, CancellationToken ct)
    {
        var note = await appointmentNoteService.UpsertAsync(id, currentUser.UserId, dto, ct);
        return Ok(note);
    }

    /// <summary>Update the private doctor note for an appointment.</summary>
    [Tags("Doctor — Appointments")]
    [HttpPut("{id:guid}/notes")]
    [Authorize(Policy = "DoctorOnly")]
    [ProducesResponseType(typeof(AppointmentNoteResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateNote(Guid id, [FromBody] UpsertAppointmentNoteDto dto, CancellationToken ct)
    {
        var note = await appointmentNoteService.UpsertAsync(id, currentUser.UserId, dto, ct);
        return Ok(note);
    }

    // ─── Calendar Views (FR-022) ──────────────────────────────────────────────

    /// <summary>Day view: all appointments for a single date, grouped (FR-022).</summary>
    [Tags("Doctor — Appointments", "Admin — Appointments")]
    [HttpGet("calendar/day")]
    [RequireRole("Doctor", "Admin")]
    [ProducesResponseType(typeof(CalendarSyncResponseDto<List<CalendarDayDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalendarDay([FromQuery] DateOnly date, CancellationToken ct)
    {
        var filter = new AppointmentFilterDto(null, date, date, null, 1, 200);
        var result = await appointmentService.GetAppointmentsAsync(currentUser.UserId, currentUser.UserType, currentUser.TenantId, filter);
        var grouped = result.Items.GroupBy(a => DateOnly.FromDateTime(a.DateTime))
            .Select(g => new CalendarDayDto(g.Key, g.OrderBy(a => a.DateTime).ToList()))
            .ToList();
        return Ok(new CalendarSyncResponseDto<List<CalendarDayDto>>(grouped, DateTime.UtcNow));
    }

    /// <summary>Week view: appointments from startDate to startDate+6 days, grouped by day (FR-022).</summary>
    [Tags("Doctor — Appointments", "Admin — Appointments")]
    [HttpGet("calendar/week")]
    [RequireRole("Doctor", "Admin")]
    [ProducesResponseType(typeof(CalendarSyncResponseDto<List<CalendarDayDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalendarWeek([FromQuery] DateOnly startDate, CancellationToken ct)
    {
        var endDate = startDate.AddDays(6);
        var filter = new AppointmentFilterDto(null, startDate, endDate, null, 1, 500);
        var result = await appointmentService.GetAppointmentsAsync(currentUser.UserId, currentUser.UserType, currentUser.TenantId, filter);
        var grouped = result.Items.GroupBy(a => DateOnly.FromDateTime(a.DateTime))
            .Select(g => new CalendarDayDto(g.Key, g.OrderBy(a => a.DateTime).ToList()))
            .ToList();
        return Ok(new CalendarSyncResponseDto<List<CalendarDayDto>>(grouped, DateTime.UtcNow));
    }

    /// <summary>Doctor view: appointments for a specific doctor over a date range (FR-022).</summary>
    [Tags("Doctor — Appointments", "Admin — Appointments")]
    [HttpGet("calendar/doctor/{doctorId:guid}")]
    [RequireRole("Doctor", "Admin")]
    [ProducesResponseType(typeof(CalendarSyncResponseDto<List<CalendarDayDto>>), StatusCodes.Status200OK)]
    public async Task<IActionResult> CalendarDoctor(Guid doctorId, [FromQuery] DateOnly startDate, [FromQuery] DateOnly? endDate, CancellationToken ct)
    {
        if (currentUser.UserType == "Doctor" && currentUser.UserId != doctorId)
            return Forbid();

        var end = endDate ?? startDate.AddDays(6);
        var filter = new AppointmentFilterDto(null, startDate, end, doctorId, 1, 500);
        var result = await appointmentService.GetAppointmentsAsync(currentUser.UserId, currentUser.UserType, currentUser.TenantId, filter);
        var grouped = result.Items.GroupBy(a => DateOnly.FromDateTime(a.DateTime))
            .Select(g => new CalendarDayDto(g.Key, g.OrderBy(a => a.DateTime).ToList()))
            .ToList();
        return Ok(new CalendarSyncResponseDto<List<CalendarDayDto>>(grouped, DateTime.UtcNow));
    }
}
