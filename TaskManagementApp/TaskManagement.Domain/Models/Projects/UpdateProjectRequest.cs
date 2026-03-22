using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Projects;

public class UpdateProjectRequest
{
    [MinLength(2)]
    public string? Name { get; set; }

    public string? Description { get; set; }
}
