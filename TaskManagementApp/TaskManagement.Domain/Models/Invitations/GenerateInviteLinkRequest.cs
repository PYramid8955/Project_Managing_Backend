namespace TaskManagement.Domain.Models.Invitations;

public class GenerateInviteLinkRequest
{
    public int? MaxUses { get; set; }
    public DateTime? ExpiresAt { get; set; }
}
