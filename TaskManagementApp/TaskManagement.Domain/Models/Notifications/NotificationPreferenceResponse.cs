namespace TaskManagement.Domain.Models.Notifications;

public class NotificationPreferenceResponse
{
    public bool RoleChanges { get; set; }
    public bool TaskReviews { get; set; }
    public bool TaskAssigned { get; set; }
    public bool Comments { get; set; }
    public bool ProjectMembership { get; set; }
}
