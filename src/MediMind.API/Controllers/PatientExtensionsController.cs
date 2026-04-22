using FluentValidation;
using MediMind.API.Attributes;
using MediMind.Application.Features.EmergencyContacts;
using MediMind.Application.Features.Favorites;
using MediMind.Application.Features.MedicalHistory;
using MediMind.Application.Features.NotificationPreferences;
using MediMind.Application.Features.Reviews;
using MediMind.Domain.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers;

// ═══════════════════════════════════════════════════════════════════════════════
// PATIENT MEDICAL HISTORY
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Structured medical background for the authenticated patient.</summary>
[ApiController]
[Authorize(Policy = "PatientOnly")]
[Tags("Patient — Medical History")]
[Route("api/v1/patients/me/medical-history")]
public class PatientMedicalHistoryController(
    IMedicalHistoryService medicalHistoryService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>Get the authenticated patient's medical history.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MedicalHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken ct)
    {
        var result = await medicalHistoryService.GetAsync(currentUser.UserId, ct);
        if (result is null)
            return NotFound(new { message = "No medical history found. Use PUT to create one." });
        return Ok(result);
    }

    /// <summary>Create or update the authenticated patient's medical history.</summary>
    [HttpPut]
    [ProducesResponseType(typeof(MedicalHistoryResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Upsert([FromBody] UpsertMedicalHistoryDto dto, CancellationToken ct)
    {
        var result = await medicalHistoryService.UpsertAsync(currentUser.UserId, dto, ct);
        return Ok(result);
    }

    /// <summary>Clear (reset) the authenticated patient's medical history, keeping the record row.</summary>
    [HttpDelete]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Clear(CancellationToken ct)
    {
        await medicalHistoryService.ClearAsync(currentUser.UserId, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// EMERGENCY CONTACTS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Emergency contacts for the authenticated patient (max 3, exactly one primary).</summary>
[ApiController]
[Authorize(Policy = "PatientOnly")]
[Tags("Patient — Emergency Contacts")]
[Route("api/v1/patients/me/emergency-contacts")]
public class EmergencyContactsController(
    IEmergencyContactService contactService,
    IValidator<CreateEmergencyContactDto> createValidator,
    IValidator<UpdateEmergencyContactDto> updateValidator,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List all emergency contacts for the authenticated patient.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<EmergencyContactResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken ct) =>
        Ok(await contactService.GetAllAsync(currentUser.UserId, ct));

    /// <summary>Add a new emergency contact (max 3 per patient).</summary>
    [HttpPost]
    [ProducesResponseType(typeof(EmergencyContactResponseDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateEmergencyContactDto dto, CancellationToken ct)
    {
        await createValidator.ValidateAndThrowAsync(dto, ct);
        var result = await contactService.CreateAsync(currentUser.UserId, dto, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Update an existing emergency contact.</summary>
    [HttpPut("{contactId:guid}")]
    [ProducesResponseType(typeof(EmergencyContactResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid contactId, [FromBody] UpdateEmergencyContactDto dto, CancellationToken ct)
    {
        await updateValidator.ValidateAndThrowAsync(dto, ct);
        var result = await contactService.UpdateAsync(currentUser.UserId, contactId, dto, ct);
        return Ok(result);
    }

    /// <summary>Delete an emergency contact.</summary>
    [HttpDelete("{contactId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid contactId, CancellationToken ct)
    {
        await contactService.DeleteAsync(currentUser.UserId, contactId, ct);
        return NoContent();
    }

    /// <summary>Set a contact as the primary emergency contact.</summary>
    [HttpPost("{contactId:guid}/set-primary")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetPrimary(Guid contactId, CancellationToken ct)
    {
        await contactService.SetPrimaryAsync(currentUser.UserId, contactId, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// FAVORITES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Patient favourite doctors and healthcare centers.</summary>
[ApiController]
[Authorize(Policy = "PatientOnly")]
[Tags("Patient — Favorites")]
[Route("api/v1/favorites")]
public class FavoritesController(
    IFavoriteService favoriteService,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List the patient's favourite doctors.</summary>
    [HttpGet("doctors")]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteDoctorResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetDoctors(CancellationToken ct) =>
        Ok(await favoriteService.GetDoctorFavoritesAsync(currentUser.UserId, ct));

    /// <summary>Add a doctor to favourites.</summary>
    [HttpPost("doctors/{doctorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddDoctor(Guid doctorId, CancellationToken ct)
    {
        await favoriteService.AddDoctorFavoriteAsync(currentUser.UserId, doctorId, ct);
        return NoContent();
    }

    /// <summary>Remove a doctor from favourites.</summary>
    [HttpDelete("doctors/{doctorId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveDoctor(Guid doctorId, CancellationToken ct)
    {
        await favoriteService.RemoveDoctorFavoriteAsync(currentUser.UserId, doctorId, ct);
        return NoContent();
    }

    /// <summary>List the patient's favourite healthcare centers.</summary>
    [HttpGet("centers")]
    [ProducesResponseType(typeof(IReadOnlyList<FavoriteCenterResponseDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCenters(CancellationToken ct) =>
        Ok(await favoriteService.GetCenterFavoritesAsync(currentUser.UserId, ct));

    /// <summary>Add a healthcare center to favourites.</summary>
    [HttpPost("centers/{centerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddCenter(Guid centerId, CancellationToken ct)
    {
        await favoriteService.AddCenterFavoriteAsync(currentUser.UserId, centerId, ct);
        return NoContent();
    }

    /// <summary>Remove a healthcare center from favourites.</summary>
    [HttpDelete("centers/{centerId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> RemoveCenter(Guid centerId, CancellationToken ct)
    {
        await favoriteService.RemoveCenterFavoriteAsync(currentUser.UserId, centerId, ct);
        return NoContent();
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// NOTIFICATION PREFERENCES
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>Per-user notification channel preferences.</summary>
[ApiController]
[Authorize]
[Tags("Patient — Notifications")]
[Route("api/v1/notifications")]
public class NotificationPreferencesController(
    INotificationPreferenceService preferenceService,
    ICurrentUser currentUser,
    INotificationLogRepository logRepository,
    IUnitOfWork unitOfWork) : ControllerBase
{
    /// <summary>Get the caller's notification preferences (created with defaults if not set).</summary>
    [HttpGet("preferences")]
    [ProducesResponseType(typeof(NotificationPreferenceResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPreferences(CancellationToken ct) =>
        Ok(await preferenceService.GetOrCreateAsync(currentUser.UserId, ct));

    /// <summary>Update the caller's notification preferences.</summary>
    [HttpPut("preferences")]
    [ProducesResponseType(typeof(NotificationPreferenceResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferenceDto dto, CancellationToken ct) =>
        Ok(await preferenceService.UpdateAsync(currentUser.UserId, dto, ct));

    /// <summary>Get the unread notification count for the authenticated user.</summary>
    [HttpGet("unread-count")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UnreadCount(CancellationToken ct)
    {
        var count = await logRepository.GetUnreadCountAsync(currentUser.UserId, ct);
        return Ok(new { unreadCount = count });
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPost("{id:guid}/mark-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var items = await logRepository.GetRecentForUserAsync(currentUser.UserId, 200, ct);
        var item = items.FirstOrDefault(n => n.Id == id);
        if (item is null)
            return NotFound();
        item.MarkRead();
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Mark all notifications as read for the authenticated user.</summary>
    [HttpPost("mark-all-read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        var items = await logRepository.GetRecentForUserAsync(currentUser.UserId, 200, ct);
        foreach (var item in items.Where(n => !n.IsRead))
            item.MarkRead();
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }
}
