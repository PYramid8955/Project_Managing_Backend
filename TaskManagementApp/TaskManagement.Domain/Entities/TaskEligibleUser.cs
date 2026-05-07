namespace TaskManagement.Domain.Entities;

public class TaskEligibleUser
{
    public Guid TaskId { get; set; }
    public Guid UserId { get; set; }

    // Navigation properties
    public AppTask Task { get; set; } = null!;
    public User User { get; set; } = null!;
}
