namespace TaskManagement.Domain.Models.Notifications;

public class UpdateNotificationPreferenceRequest
{
    public bool RoleChanges { get; set; } = true;
    public bool TaskReviews { get; set; } = true;
    public bool TaskAssigned { get; set; } = true;
    public bool Comments { get; set; } = true;
    public bool ProjectMembership { get; set; } = true;
}
