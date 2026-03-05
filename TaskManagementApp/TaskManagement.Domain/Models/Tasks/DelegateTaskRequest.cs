using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Tasks;

public class DelegateTaskRequest
{
    [Required]
    public Guid DeveloperUserId { get; set; }
}
