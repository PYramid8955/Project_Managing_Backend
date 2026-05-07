using TaskManagement.Domain.Models.Notifications;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface INotificationService
{
    Task CreateAsync(Guid userId, string type, string title, string message, Guid? entityId = null, string? entityType = null);
    Task<IEnumerable<NotificationResponse>> GetUserNotificationsAsync(Guid userId);
    Task MarkReadAsync(Guid notificationId, Guid userId);
    Task MarkAllReadAsync(Guid userId);
    Task DeleteAsync(Guid notificationId, Guid userId);
    Task ClearAllAsync(Guid userId);
    Task<NotificationPreferenceResponse> GetPreferencesAsync(Guid userId);
    Task UpdatePreferencesAsync(Guid userId, UpdateNotificationPreferenceRequest request);
    Task<bool> ShouldNotifyAsync(Guid userId, string type);
}
