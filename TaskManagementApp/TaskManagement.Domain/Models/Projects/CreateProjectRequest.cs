using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Projects;

public class CreateProjectRequest
{
    [Required]
    [MinLength(2)]
    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;
}
