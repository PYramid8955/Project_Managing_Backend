using TaskManagement.Domain.Models.Invitations;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IProjectInvitationService
{
    Task<InvitationResponse> SendInvitationAsync(Guid projectId, SendInvitationRequest request, Guid invitedById);
    Task<GenerateInviteLinkResponse> GenerateInviteLinkAsync(Guid projectId, Guid invitedById, string frontendBaseUrl, GenerateInviteLinkRequest? options = null);
    Task<IEnumerable<InvitationResponse>> GetProjectInvitationsAsync(Guid projectId, Guid requestingUserId);
    Task<IEnumerable<InvitationResponse>> GetLinkInvitationsAsync(Guid projectId, Guid requestingUserId);
    Task CancelInvitationAsync(Guid projectId, Guid invitationId, Guid requestingUserId);
    Task DeleteInviteLinkAsync(Guid projectId, Guid invitationId, Guid requestingUserId);
    Task<IEnumerable<InvitationResponse>> GetMyInvitationsAsync(Guid userId);
    Task<InvitationResponse> GetInvitationByTokenAsync(string token);
    Task AcceptInvitationAsync(string token, Guid userId);
    Task DeclineInvitationAsync(string token, Guid userId);
}
