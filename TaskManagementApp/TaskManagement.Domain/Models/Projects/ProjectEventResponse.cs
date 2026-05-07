namespace TaskManagement.Domain.Models.Projects;

public class ProjectEventResponse
{
    public Guid Id { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ActorUsername { get; set; }
    public string? TargetUsername { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class ProjectDayStats
{
    public int MembersJoined { get; set; }
    public int MembersLeft { get; set; }
    public int RolesChanged { get; set; }
    public int InviteLinksCreated { get; set; }
    public int InviteLinksDeleted { get; set; }
    public bool ProjectUpdated { get; set; }
}

public class ProjectHistoryDayResponse
{
    public DateTime Date { get; set; }
    public IEnumerable<ProjectEventResponse> Events { get; set; } = [];
    public ProjectDayStats Stats { get; set; } = new();
}
