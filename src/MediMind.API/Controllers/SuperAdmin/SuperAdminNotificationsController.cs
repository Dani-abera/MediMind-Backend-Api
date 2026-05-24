using MediMind.Domain.Common.Interfaces;
using MediMind.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace MediMind.API.Controllers.SuperAdmin;

[ApiController]
[Route("api/v1/super-admin/notifications")]
[Authorize(Policy = "SuperAdminOnly")]
[Tags("SuperAdmin — Notifications")]
public class SuperAdminNotificationsController(
    INotificationLogRepository logRepository,
    IUnitOfWork unitOfWork,
    ICurrentUser currentUser) : ControllerBase
{
    /// <summary>List paginated notification history for the authenticated super admin.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var (items, total) = await logRepository.GetPagedAsync(currentUser.UserId, page, pageSize, ct);
        var dto = items.Select(n => new SuperAdminNotificationDto(
            n.Id, n.NotificationType, n.Channel, n.Title, n.Body, n.Status, n.SentAt, n.IsRead));
        return Ok(new { items = dto, totalCount = total, page, pageSize });
    }

    /// <summary>Mark a single notification as read.</summary>
    [HttpPatch("{id:guid}/read")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken ct)
    {
        var log = await logRepository.GetByIdForUserAsync(id, currentUser.UserId, ct);
        if (log is null) return NotFound();
        log.MarkRead();
        await unitOfWork.SaveChangesAsync(ct);
        return NoContent();
    }

    /// <summary>Mark all notifications as read for the authenticated super admin.</summary>
    [HttpPatch("read-all")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> MarkAllRead(CancellationToken ct)
    {
        await logRepository.MarkAllReadAsync(currentUser.UserId, ct);
        return NoContent();
    }
}

public record SuperAdminNotificationDto(
    Guid Id,
    string NotificationType,
    string Channel,
    string? Title,
    string Body,
    string Status,
    DateTime SentAt,
    bool IsRead);
