using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Projects;

public class CreateProjectRequest
{
    [Required]
    [MinLength(2)]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;
}
