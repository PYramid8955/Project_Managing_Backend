namespace TaskManagement.Domain.Entities;

public class TaskSubmission
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public Guid SubmittedById { get; set; }
    public string? FileUrl { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public AppTask Task { get; set; } = null!;
    public User SubmittedBy { get; set; } = null!;
    public TaskReview? Review { get; set; }
}
