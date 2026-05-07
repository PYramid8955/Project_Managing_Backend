using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.Domain.Models.Notifications;

namespace TaskManagement.Api.Controllers;

[Route("api/notifications")]
[Authorize]
public class NotificationsController : BaseController
{
    private readonly INotificationService _notifications;

    public NotificationsController(INotificationService notifications)
    {
        _notifications = notifications;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var result = await _notifications.GetUserNotificationsAsync(GetUserId());
        return Ok(result);
    }

    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        await _notifications.MarkReadAsync(id, GetUserId());
        return NoContent();
    }

    [HttpPatch("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        await _notifications.MarkAllReadAsync(GetUserId());
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        await _notifications.DeleteAsync(id, GetUserId());
        return NoContent();
    }

    [HttpDelete("clear")]
    public async Task<IActionResult> ClearAll()
    {
        await _notifications.ClearAllAsync(GetUserId());
        return NoContent();
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var result = await _notifications.GetPreferencesAsync(GetUserId());
        return Ok(result);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferenceRequest request)
    {
        await _notifications.UpdatePreferencesAsync(GetUserId(), request);
        return NoContent();
    }
}
