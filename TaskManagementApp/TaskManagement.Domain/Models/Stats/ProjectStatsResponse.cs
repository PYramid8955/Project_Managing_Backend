namespace TaskManagement.Domain.Models.Stats;

public class ProjectStatsResponse
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public int TotalTasks { get; set; }
    public int ApprovedTasks { get; set; }
    public int PendingTasks { get; set; }
    public int RejectedTasks { get; set; }
    public double ApprovalRate { get; set; }
    public int TotalPoints { get; set; }
    public int EasyTasks { get; set; }
    public int MediumTasks { get; set; }
    public int HardTasks { get; set; }
    public int OverdueTasks { get; set; }
}
