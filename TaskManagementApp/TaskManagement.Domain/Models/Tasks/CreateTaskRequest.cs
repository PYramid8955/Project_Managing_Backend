using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Tasks;

public class CreateTaskRequest
{
    [Required]
    [MinLength(3)]
    [MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    public Guid? AssignedToId { get; set; }

    [Required]
    public TaskDifficulty Difficulty { get; set; }

    public DateTime? DueDate { get; set; }

    public AssignmentMode AssignmentMode { get; set; } = AssignmentMode.Open;

    public bool RequiresAttachment { get; set; } = false;

    /// <summary>Used when AssignmentMode = Invited. List of eligible developer user IDs.</summary>
    public List<Guid> EligibleUserIds { get; set; } = new();
}
