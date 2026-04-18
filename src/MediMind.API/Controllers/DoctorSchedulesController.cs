using MediMind.API.Attributes;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
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

/// <summary>
/// Doctor schedule management endpoints.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/doctor-schedules")]
[Tags("Doctor schedules")]
public class DoctorSchedulesController(IDoctorScheduleRepository scheduleRepository, ICurrentUser currentUser) : ControllerBase
{
    /// <summary>
    /// Creates or updates doctor schedule.
    /// </summary>
    [HttpPost]
    [RequireRole("Admin")]
    public async Task<IActionResult> Upsert([FromBody] UpsertDoctorScheduleDto dto)
    {
        if (currentUser.TenantId != dto.CenterId)
            return Forbid();

        if (dto.EndTime <= dto.StartTime)
            return UnprocessableEntity(new { error = "EndTime must be greater than StartTime" });

        if (dto.BreakStart.HasValue && dto.BreakEnd.HasValue)
        {
            if (dto.BreakEnd <= dto.BreakStart)
                return UnprocessableEntity(new { error = "BreakEnd must be greater than BreakStart" });
            if (dto.BreakStart < dto.StartTime || dto.BreakEnd > dto.EndTime)
                return UnprocessableEntity(new { error = "Break must be within working hours" });
        }

        var existing = await scheduleRepository.GetByDoctorAndCenterAsync(dto.DoctorId, dto.CenterId);
        if (existing is null)
        {
            var schedule = new DoctorSchedule(dto.DoctorId, dto.CenterId, dto.WorkingDays, dto.StartTime, dto.EndTime, dto.SlotDuration, dto.BreakStart, dto.BreakEnd);
            var created = await scheduleRepository.CreateAsync(schedule);
            return Ok(new { created.ScheduleId });
        }

        var updated = new DoctorSchedule(dto.DoctorId, dto.CenterId, dto.WorkingDays, dto.StartTime, dto.EndTime, dto.SlotDuration, dto.BreakStart, dto.BreakEnd);
        await scheduleRepository.DeleteAsync(existing.Id);
        var recreated = await scheduleRepository.CreateAsync(updated);
        return Ok(new { recreated.ScheduleId });
    }

    /// <summary>
    /// Gets doctor schedule by doctor and center.
    /// </summary>
    [HttpGet("{doctorId:guid}/{centerId:guid}")]
    [RequireRole("Doctor", "Admin")]
    public async Task<IActionResult> Get(Guid doctorId, Guid centerId)
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

    /// <summary>
    /// Deletes doctor schedule.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [RequireRole("Admin")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await scheduleRepository.DeleteAsync(id);
        return deleted ? NoContent() : NotFound(new { error = "Schedule not found" });
    }
}
