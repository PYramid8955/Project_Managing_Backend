using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Tasks;

public class UpdateTaskRequest
{
    [MinLength(3)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    public TaskDifficulty? Difficulty { get; set; }

    public DateTime? DueDate { get; set; }
}
