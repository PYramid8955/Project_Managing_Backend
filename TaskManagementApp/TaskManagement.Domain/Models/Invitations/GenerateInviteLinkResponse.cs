namespace TaskManagement.Domain.Models.Invitations;

public class GenerateInviteLinkResponse
{
    public Guid InvitationId { get; set; }
    public string Token { get; set; } = "";
    public string Link { get; set; } = "";
    public DateTime? ExpiresAt { get; set; }
    public int? MaxUses { get; set; }
    public int UseCount { get; set; }
}
