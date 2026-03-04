using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class ProjectMember
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid ProjectId { get; set; }
    public ProjectRole Role { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// For Developers: the UserId of the Manager they belong to in this project.
    /// Null means the developer has no assigned manager yet.
    /// Managers and Admins always have this as null.
    /// </summary>
    public Guid? ManagerUserId { get; set; }

    // Navigation properties
    public User User { get; set; } = null!;
    public Project Project { get; set; } = null!;
}
