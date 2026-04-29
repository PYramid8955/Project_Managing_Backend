namespace TaskManagement.Domain.Entities;

public class ProjectEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid? ActorId { get; set; }
    public Guid? TargetUserId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Project Project { get; set; } = null!;
    public User? Actor { get; set; }
    public User? TargetUser { get; set; }
}
