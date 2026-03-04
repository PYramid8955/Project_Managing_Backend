using TaskManagement.Domain.Enums;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Domain.Entities;

public class AppTask
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid CreatedById { get; set; }
    public Guid? AssignedToId { get; set; }
    public TaskDifficulty Difficulty { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.ToDo;
    public AssignmentMode AssignmentMode { get; set; } = AssignmentMode.Open;
    public bool RequiresAttachment { get; set; } = false;
    public string? RejectionFeedback { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DueDate { get; set; }
    public string? ImageUrl { get; set; }

    // Navigation properties
    public Project Project { get; set; } = null!;
    public User CreatedBy { get; set; } = null!;
    public User? AssignedTo { get; set; }
    public ICollection<TaskSubmission> Submissions { get; set; } = new List<TaskSubmission>();
    public ICollection<TaskComment> Comments { get; set; } = new List<TaskComment>();
    public ICollection<TaskEligibleUser> EligibleUsers { get; set; } = new List<TaskEligibleUser>();
}
