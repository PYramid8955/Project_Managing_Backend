using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Entities;

public class ProjectInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Guid InvitedById { get; set; }

    /// <summary>Null for link-based invitations (open to any authenticated user).</summary>
    public Guid? InvitedUserId { get; set; }

    /// <summary>Null for link-based invitations; role is assigned by Admin after join.</summary>
    public ProjectRole? Role { get; set; }

    public string Token { get; set; } = Guid.NewGuid().ToString("N");
    public InvitationStatus Status { get; set; } = InvitationStatus.Pending;
    public bool IsLinkInvitation { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddDays(7);

    // Navigation
    public Project Project { get; set; } = null!;
    public User InvitedBy { get; set; } = null!;
    public User? InvitedUser { get; set; }
}
