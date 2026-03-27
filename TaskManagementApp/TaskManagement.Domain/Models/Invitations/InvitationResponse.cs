using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Invitations;

public class InvitationResponse
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = "";
    public string InvitedByUsername { get; set; } = "";
    public string? InvitedUserEmail { get; set; }
    public string? InvitedUsername { get; set; }
    public ProjectRole? Role { get; set; }
    public string Token { get; set; } = "";
    public InvitationStatus Status { get; set; }
    public bool IsLinkInvitation { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
