using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Models.Notifications;

namespace TaskManagement.BusinessLayer.Core;

public class NotificationService : INotificationService
{
    private readonly DbSession _db;

    public NotificationService(DbSession db)
    {
        _db = db;
    }

    public async Task CreateAsync(Guid userId, string type, string title, string message, Guid? entityId = null, string? entityType = null)
    {
        if (!await ShouldNotifyAsync(userId, type)) return;

        await _db.GetRepo<Notification>().AddAsync(new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            EntityId = entityId,
            EntityType = entityType
        });
        await _db.SaveAsync();
    }

    public async Task<IEnumerable<NotificationResponse>> GetUserNotificationsAsync(Guid userId)
    {
        var notifications = await _db.GetRepo<Notification>().Query()
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();

        return notifications.Select(n => new NotificationResponse
        {
            Id = n.Id,
            Type = n.Type,
            Title = n.Title,
            Message = n.Message,
            EntityId = n.EntityId,
            EntityType = n.EntityType,
            IsRead = n.IsRead,
            CreatedAt = n.CreatedAt
        });
    }

    public async Task MarkReadAsync(Guid notificationId, Guid userId)
    {
        var n = await _db.GetRepo<Notification>().GetByIdAsync(notificationId);
        if (n == null || n.UserId != userId) return;
        n.IsRead = true;
        _db.GetRepo<Notification>().Update(n);
        await _db.SaveAsync();
    }

    public async Task MarkAllReadAsync(Guid userId)
    {
        var unread = await _db.GetRepo<Notification>().FindAsync(n => n.UserId == userId && !n.IsRead);
        foreach (var n in unread) { n.IsRead = true; _db.GetRepo<Notification>().Update(n); }
        await _db.SaveAsync();
    }

    public async Task DeleteAsync(Guid notificationId, Guid userId)
    {
        var n = await _db.GetRepo<Notification>().GetByIdAsync(notificationId);
        if (n == null || n.UserId != userId) return;
        _db.GetRepo<Notification>().Delete(n);
        await _db.SaveAsync();
    }

    public async Task ClearAllAsync(Guid userId)
    {
        var all = await _db.GetRepo<Notification>().FindAsync(n => n.UserId == userId);
        foreach (var n in all) _db.GetRepo<Notification>().Delete(n);
        await _db.SaveAsync();
    }

    public async Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId)
    {
        var pref = await GetOrCreatePrefsAsync(userId);
        return ToResponse(pref);
    }

    public async Task UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request)
    {
        var pref = await GetOrCreatePrefsAsync(userId);
        pref.RoleChanges = request.RoleChanges;
        pref.TaskReviews = request.TaskReviews;
        pref.TaskAssigned = request.TaskAssigned;
        pref.Comments = request.Comments;
        pref.ProjectMembership = request.ProjectMembership;
        _db.GetRepo<NotificationPreference>().Update(pref);
        await _db.SaveAsync();
    }

    public async Task<bool> ShouldNotifyAsync(Guid userId, string type)
    {
        var pref = await GetOrCreatePrefsAsync(userId);
        return type switch
        {
            NotificationType.RoleAssigned or NotificationType.RoleChanged => pref.RoleChanges,
            NotificationType.RemovedFromProject or NotificationType.MemberLeft => pref.ProjectMembership,
            NotificationType.TaskAssigned or NotificationType.TaskDelegated => pref.TaskAssigned,
            NotificationType.TaskApproved or NotificationType.TaskRejected or NotificationType.TaskSubmitted => pref.TaskReviews,
            NotificationType.CommentAdded => pref.Comments,
            _ => true
        };
    }

    private async Task<NotificationPreference> GetOrCreatePrefsAsync(Guid userId)
    {
        var existing = await _db.GetRepo<NotificationPreference>().FindAsync(p => p.UserId == userId);
        var pref = existing.FirstOrDefault();
        if (pref != null) return pref;

        pref = new NotificationPreference { UserId = userId };
        await _db.GetRepo<NotificationPreference>().AddAsync(pref);
        await _db.SaveAsync();
        return pref;
    }

    private static NotificationPreferenceResponse ToResponse(NotificationPreference p) => new()
    {
        RoleChanges = p.RoleChanges,
        TaskReviews = p.TaskReviews,
        TaskAssigned = p.TaskAssigned,
        Comments = p.Comments,
        ProjectMembership = p.ProjectMembership
    };
}
