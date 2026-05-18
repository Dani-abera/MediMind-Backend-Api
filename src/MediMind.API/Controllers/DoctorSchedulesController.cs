using MediMind.API.Attributes;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using MediMind.Domain.Enums;
using MediMind.Domain.Exceptions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

public record UpsertDoctorScheduleDto(
    Guid DoctorId,
    Guid CenterId,
    List<string> WorkingDays,
    TimeOnly StartTime,
    TimeOnly EndTime,
    int SlotDuration,
    TimeOnly? BreakStart,
    TimeOnly? BreakEnd);

public record AddScheduleExceptionDto(DateOnly Date, string Reason);

/// <summary>
/// Doctor schedule management endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/doctor-schedules")]
[Tags("Admin — Schedules")]
public class DoctorSchedulesController(
    IDoctorScheduleRepository scheduleRepository,
    IScheduleExceptionRepository exceptionRepository,
    IAppointmentRepository appointmentRepository,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Create or replace a doctor's weekly working schedule (FR-020).</summary>
    [HttpPost]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Upsert([FromBody] UpsertDoctorScheduleDto dto, CancellationToken ct)
    {
        if (currentUser.TenantId != dto.CenterId)
            return Forbid();

        if (dto.EndTime <= dto.StartTime)
            return UnprocessableEntity(new { error = "EndTime must be greater than StartTime" });

        if (dto.BreakStart.HasValue && dto.BreakEnd.HasValue)
        {
            if (dto.BreakEnd <= dto.BreakStart)
                return UnprocessableEntity(new { error = "End time must be after start time" });
            if (dto.BreakStart < dto.StartTime || dto.BreakEnd > dto.EndTime)
                return UnprocessableEntity(new { error = $"Break time must be within working hours [{dto.StartTime:HH\\:mm}-{dto.EndTime:HH\\:mm}]" });
        }

        var existing = await scheduleRepository.GetByDoctorAndCenterAsync(dto.DoctorId, dto.CenterId);
        if (existing is null)
        {
            var schedule = new DoctorSchedule(dto.DoctorId, dto.CenterId, dto.WorkingDays, dto.StartTime, dto.EndTime, dto.SlotDuration, dto.BreakStart, dto.BreakEnd);
            var created = await scheduleRepository.CreateAsync(schedule);
            return Ok(new { created.ScheduleId });
        }

        // Protect future confirmed appointments — do not silently orphan them
        var futureFilter = new AppointmentFilterDto(AppointmentStatus.Confirmed, DateOnly.FromDateTime(DateTime.UtcNow), null, dto.DoctorId, 1, 1);
        var futureConflicts = await appointmentRepository.GetByDoctorAsync(dto.DoctorId, dto.CenterId, futureFilter);
        if (futureConflicts.TotalCount > 0)
            return UnprocessableEntity(new { error = "Cannot modify schedule: future confirmed appointments exist. Cancel or reschedule them first." });

        var updated = new DoctorSchedule(dto.DoctorId, dto.CenterId, dto.WorkingDays, dto.StartTime, dto.EndTime, dto.SlotDuration, dto.BreakStart, dto.BreakEnd);
        await scheduleRepository.DeleteAsync(existing.Id);
        var recreated = await scheduleRepository.CreateAsync(updated);
        return Ok(new { recreated.ScheduleId });
    }

    /// <summary>Get a doctor's schedule for a specific center (FR-020).</summary>
    [HttpGet("{doctorId:guid}/{centerId:guid}")]
    [RequireRole("Doctor", "Admin")]
    [ProducesResponseType(typeof(DoctorSchedule), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(Guid doctorId, Guid centerId, CancellationToken ct)
    {
        if (currentUser.UserType == "Doctor" && currentUser.UserId != doctorId)
            throw new UnauthorizedException();
        if (currentUser.UserType == "Admin" && currentUser.TenantId != centerId)
            throw new UnauthorizedException();

        var schedule = await scheduleRepository.GetByDoctorAndCenterAsync(doctorId, centerId);
        if (schedule is null)
            return NotFound(new { error = "Schedule not found" });

        return Ok(schedule);
    }

    /// <summary>Delete a doctor's schedule by ID (FR-020).</summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var deleted = await scheduleRepository.DeleteAsync(id);
        return deleted ? NoContent() : NotFound(new { error = "Schedule not found" });
    }

    // ── Schedule Exceptions (out-of-office / specific date unavailability) ──────

    /// <summary>List all exception dates for a doctor at a center (FR-021).</summary>
    [HttpGet("{doctorId:guid}/{centerId:guid}/exceptions")]
    [RequireRole("Doctor", "Admin")]
    [ProducesResponseType(typeof(IReadOnlyList<ScheduleException>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetExceptions(Guid doctorId, Guid centerId, CancellationToken ct)
    {
        if (currentUser.UserType == "Doctor" && currentUser.UserId != doctorId)
            throw new UnauthorizedException();
        if (currentUser.UserType == "Admin" && currentUser.TenantId != centerId)
            throw new UnauthorizedException();

        var exceptions = await exceptionRepository.GetByDoctorAndCenterAsync(doctorId, centerId, ct);
        return Ok(exceptions);
    }

    /// <summary>Mark a specific date as unavailable for a doctor (FR-021).</summary>
    [HttpPost("{doctorId:guid}/{centerId:guid}/exceptions")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> AddException(Guid doctorId, Guid centerId, [FromBody] AddScheduleExceptionDto dto, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId)
            return Forbid();

        if (await exceptionRepository.ExistsAsync(doctorId, centerId, dto.Date, ct))
            return Conflict(new { error = "An exception already exists for this date." });

        var exception = new ScheduleException(doctorId, centerId, dto.Date, dto.Reason);
        await exceptionRepository.CreateAsync(exception, ct);
        return StatusCode(StatusCodes.Status201Created, new { exception.Id, exception.ExceptionDate });
    }

    /// <summary>Remove an exception date, making the day available again (FR-021).</summary>
    [HttpDelete("{doctorId:guid}/{centerId:guid}/exceptions/{date}")]
    [RequireRole("Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> RemoveException(Guid doctorId, Guid centerId, DateOnly date, CancellationToken ct)
    {
        if (currentUser.TenantId != centerId)
            return Forbid();

        var deleted = await exceptionRepository.DeleteAsync(doctorId, centerId, date, ct);
        return deleted ? NoContent() : NotFound(new { error = "Exception not found." });
    }
}
