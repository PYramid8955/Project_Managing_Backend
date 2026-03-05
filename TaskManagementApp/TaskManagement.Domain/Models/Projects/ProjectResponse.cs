namespace TaskManagement.Domain.Models.Projects;

public class ProjectResponse
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Guid CreatedById { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public int MemberCount { get; set; }
}
