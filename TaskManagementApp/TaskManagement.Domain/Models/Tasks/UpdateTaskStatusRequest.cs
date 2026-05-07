using System.ComponentModel.DataAnnotations;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.Domain.Models.Tasks;

public class UpdateTaskStatusRequest
{
    [Required]
    public TaskStatus NewStatus { get; set; }
}
