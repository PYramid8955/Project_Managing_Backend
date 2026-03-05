namespace TaskManagement.Domain.Models.Stats;

public class UserStatsResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public int TotalProjects { get; set; }
    public int TotalAssignedTasks { get; set; }
    public int ApprovedTasks { get; set; }
    public int RejectedTasks { get; set; }
    public int TotalPoints { get; set; }
    public double ApprovalRate { get; set; }
    public List<ProjectContribution> ProjectContributions { get; set; } = new();
}

public class ProjectContribution
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int AssignedTasks { get; set; }
    public int ApprovedTasks { get; set; }
    public int Points { get; set; }
}
