using System.Text.Json;
using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

// ═══════════════════════════════════════════════════════════════════════════════
// NOTIFICATIONS (FCM + history)
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>FCM device token registration and in-app notification history.</summary>
[Authorize]
[Route("api/v1/notifications")]
[ApiController]
public class NotificationsController(
    INotificationService notificationService,
    INotificationLogRepository logRepository,
    ICurrentUser currentUser,
    IWebHostEnvironment environment) : ControllerBase
{
    /// <summary>Register or refresh an FCM device token for push notifications (FR-070).</summary>
    [Tags("Patient — Notifications")]
    [HttpPost("device-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RegisterDevice([FromBody] RegisterDeviceTokenRequest request, CancellationToken ct)
    {
        await notificationService.RegisterDeviceTokenAsync(
            currentUser.UserId,
            request.FcmToken,
            request.Platform,
            request.DeviceModel,
            ct);
        return NoContent();
    }

    /// <summary>Deactivate an FCM device token (FR-070).</summary>
    [Tags("Patient — Notifications")]
    [HttpDelete("device-token")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> DeregisterDevice([FromBody] DeregisterDeviceTokenRequest request, CancellationToken ct)
    {
        await notificationService.DeregisterDeviceTokenAsync(currentUser.UserId, request.FcmToken, ct);
        return NoContent();
    }

    /// <summary>List the last 50 notification log entries for the signed-in patient (FR-071).</summary>
    [Tags("Patient — Notifications")]
    [HttpGet("history")]
    [Authorize(Policy = "PatientOnly")]
    [ProducesResponseType(typeof(IReadOnlyList<NotificationHistoryItemDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> History(CancellationToken ct)
    {
        var items = await logRepository.GetRecentForUserAsync(currentUser.UserId, 50, ct);
        var dto = items.Select(n => new NotificationHistoryItemDto(
            n.Id,
            n.NotificationType,
            n.Channel,
            n.Title,
            n.Body,
            n.Status,
            n.SentAt)).ToList();
        return Ok(dto);
    }

    /// <summary>
    /// Development-only: send a test push notification to any user (SuperAdmin).
    /// Only available in the Development environment.
    /// </summary>
    [Tags("System")]
    [HttpPost("test-push")]
    [Authorize(Policy = "SuperAdminOnly")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> TestPush([FromBody] TestPushRequest request, CancellationToken ct)
    {
        if (!environment.IsDevelopment())
            return NotFound();

        await notificationService.SendPushAsync(request.UserId, request.Title, request.Body, null, ct);
        return NoContent();
    }
}

public record MedicationReminderDto(
    Guid Id,
    string MedicationName,
    string Dosage,
    string Frequency,
    string ReminderTimes,
    bool IsActive);

public record RegisterDeviceTokenRequest(string FcmToken, string Platform, string? DeviceModel);

public record DeregisterDeviceTokenRequest(string FcmToken);

public record TestPushRequest(Guid UserId, string Title, string Body);

public record NotificationHistoryItemDto(
    Guid LogId,
    string NotificationType,
    string Channel,
    string? Title,
    string Body,
    string Status,
    DateTime SentAt);

// ═══════════════════════════════════════════════════════════════════════════════
// MEDICATION REMINDERS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Patient medication reminder schedules.</summary>
[Tags("Patient — Medication Reminders")]
[Authorize(Policy = "PatientOnly")]
[Route("api/v1/medication-reminders")]
[ApiController]
public class MedicationRemindersController(
    IMedicationReminderRepository reminderRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ControllerBase
{
    public record CreateMedicationReminderRequest(
        string MedicationName,
        string Dosage,
        string Frequency,
        string[] ReminderTimes);

    public record UpdateMedicationReminderRequest(
        string MedicationName,
        string Dosage,
        string Frequency,
        string[] ReminderTimes);

    /// <summary>Create a new medication reminder schedule for the authenticated patient (FR-080).</summary>
    [HttpPost]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public async Task<IActionResult> Create([FromBody] CreateMedicationReminderRequest request, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(request.ReminderTimes);
        var entity = new MedicationReminder(
            currentUser.UserId,
            request.MedicationName,
            request.Dosage,
            request.Frequency,
            json);

        await reminderRepository.AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return CreatedAtAction(nameof(GetMine), new { id = entity.Id }, new { id = entity.Id });
    }

    /// <summary>List all medication reminders for the authenticated patient (FR-081).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<MedicationReminderDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine(CancellationToken ct)
    {
        var items = await reminderRepository.GetByPatientAsync(currentUser.UserId, ct);
        return Ok(items.Select(r => new MedicationReminderDto(
            r.Id, r.MedicationName, r.Dosage, r.Frequency, r.ReminderTimes, r.IsActive)));
    }

    /// <summary>Update an existing medication reminder (FR-082).</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateMedicationReminderRequest request, CancellationToken ct)
    {
        var entity = await reminderRepository.GetByIdAsync(id, currentUser.UserId, ct);
        if (entity is null)
            return NotFound();

        entity.Update(
            request.MedicationName,
            request.Dosage,
            request.Frequency,
            JsonSerializer.Serialize(request.ReminderTimes));
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Delete a medication reminder (FR-083).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var entity = await reminderRepository.GetByIdAsync(id, currentUser.UserId, ct);
        if (entity is null)
            return NotFound();

        await reminderRepository.DeleteAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Toggle a medication reminder active/inactive (FR-084).</summary>
    [HttpPatch("{id:guid}/toggle")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Toggle(Guid id, CancellationToken ct)
    {
        var entity = await reminderRepository.GetByIdAsync(id, currentUser.UserId, ct);
        if (entity is null)
            return NotFound();

        entity.SetActive(!entity.IsActive);
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }
}
