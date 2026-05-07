using TaskManagement.Domain.Enums;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Domain.Models.Tasks;

public class TaskResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid ProjectId { get; set; }
    public Guid CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public Guid? AssignedToId { get; set; }
    public string? AssignedToUsername { get; set; }
    public TaskDifficulty Difficulty { get; set; }
    public TaskStatus Status { get; set; }
    public AssignmentMode AssignmentMode { get; set; }
    public bool RequiresAttachment { get; set; }
    public string? RejectionFeedback { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? DueDate { get; set; }
    public string? ImageUrl { get; set; }
    public List<Guid> EligibleUserIds { get; set; } = new();
}
