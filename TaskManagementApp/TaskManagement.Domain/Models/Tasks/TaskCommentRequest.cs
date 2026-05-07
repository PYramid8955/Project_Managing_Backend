using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Tasks;

public class TaskCommentRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(2000)]
    public string Content { get; set; } = string.Empty;
}
