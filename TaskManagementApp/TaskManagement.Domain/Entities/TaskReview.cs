using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class TaskReview
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SubmissionId { get; set; }
    public Guid ReviewedById { get; set; }
    public ReviewStatus Status { get; set; }
    public string? Feedback { get; set; }
    public DateTime ReviewedAt { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public TaskSubmission Submission { get; set; } = null!;
    public User ReviewedBy { get; set; } = null!;
}
