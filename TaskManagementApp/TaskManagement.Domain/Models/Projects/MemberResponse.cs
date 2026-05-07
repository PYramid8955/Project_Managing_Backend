using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Projects;

public class MemberResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; }
    /// <summary>For Developers: the UserId of their assigned Manager in this project.</summary>
    public Guid? ManagerUserId { get; set; }
    public string? ManagerUsername { get; set; }
    public bool IsPendingRoleAssignment { get; set; }
}
