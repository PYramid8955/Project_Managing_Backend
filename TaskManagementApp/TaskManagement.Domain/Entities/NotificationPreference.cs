namespace TaskManagement.Domain.Entities;

public class NotificationPreference
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public bool RoleChanges { get; set; } = true;
    public bool TaskReviews { get; set; } = true;
    public bool TaskAssigned { get; set; } = true;
    public bool Comments { get; set; } = true;
    public bool ProjectMembership { get; set; } = true;

    public User User { get; set; } = null!;
}
