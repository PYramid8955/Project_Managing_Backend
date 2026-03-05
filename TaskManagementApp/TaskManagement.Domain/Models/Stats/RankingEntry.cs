namespace TaskManagement.Domain.Models.Stats;

public class RankingEntry
{
    public int Rank { get; set; }
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    /// <summary>Total points (for managers: personal + group). For developers: personal only.</summary>
    public int Points { get; set; }
    public int PersonalPoints { get; set; }
    /// <summary>Sum of group developers' points. Only relevant for Managers.</summary>
    public int GroupPoints { get; set; }
    public int ApprovedTasks { get; set; }
    public int TotalSubmissions { get; set; }
    public double ApprovalRate { get; set; }
}

public class ProjectRankingResponse
{
    public List<RankingEntry> DeveloperRanking { get; set; } = new();
    public List<RankingEntry> ManagerRanking { get; set; } = new();
}
