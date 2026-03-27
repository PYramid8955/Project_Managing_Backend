using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Invitations;

public class SendInvitationRequest
{
    [Required][EmailAddress]
    public string Email { get; set; } = "";
    public ProjectRole Role { get; set; }
}
