namespace TaskManagement.Domain.Models.Users;

public class PublicUserProfileResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarUrl { get; set; }
    public DateTime MemberSince { get; set; }
    public bool IsDeleted { get; set; }
    public int TotalApprovedTasks { get; set; }
    public int TotalPoints { get; set; }
    public List<PublicProjectContribution> SharedProjects { get; set; } = new();
}

public class PublicProjectContribution
{
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public int ApprovedTasks { get; set; }
    public int Points { get; set; }
}
