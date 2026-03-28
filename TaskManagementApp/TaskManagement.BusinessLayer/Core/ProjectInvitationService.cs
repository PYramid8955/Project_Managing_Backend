using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Invitations;

namespace TaskManagement.BusinessLayer.Core;

public class ProjectInvitationService : IProjectInvitationService
{
    private readonly DbSession _db;

    public ProjectInvitationService(DbSession db)
    {
        _db = db;
    }

    public async Task<InvitationResponse> SendInvitationAsync(Guid projectId, SendInvitationRequest request, Guid invitedById)
    {
        await RequireAdminAsync(projectId, invitedById);

        var targetUser = (await _db.GetRepo<User>().FindAsync(u => u.Email == request.Email.ToLower()))
            .FirstOrDefault() ?? throw new NotFoundException($"No user found with email '{request.Email}'.");

        if (targetUser.Id == invitedById)
            throw new ValidationException("You cannot invite yourself.");

        var alreadyMember = await _db.GetRepo<ProjectMember>()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.Id);
        if (alreadyMember) throw new ConflictException("This user is already a member of this project.");

        var pendingExists = await _db.GetRepo<ProjectInvitation>()
            .AnyAsync(i => i.ProjectId == projectId && i.InvitedUserId == targetUser.Id && i.Status == InvitationStatus.Pending);
        if (pendingExists) throw new ConflictException("A pending invitation already exists for this user.");

        var invitation = new ProjectInvitation
        {
            ProjectId = projectId,
            InvitedById = invitedById,
            InvitedUserId = targetUser.Id,
            Role = request.Role,
            IsLinkInvitation = false
        };

        await _db.GetRepo<ProjectInvitation>().AddAsync(invitation);
        await _db.SaveAsync();

        return await BuildResponseAsync(invitation);
    }

    public async Task<GenerateInviteLinkResponse> GenerateInviteLinkAsync(Guid projectId, Guid invitedById, string frontendBaseUrl)
    {
        await RequireAdminAsync(projectId, invitedById);

        var invitation = new ProjectInvitation
        {
            ProjectId = projectId,
            InvitedById = invitedById,
            IsLinkInvitation = true
        };

        await _db.GetRepo<ProjectInvitation>().AddAsync(invitation);
        await _db.SaveAsync();

        return new GenerateInviteLinkResponse
        {
            InvitationId = invitation.Id,
            Token = invitation.Token,
            Link = $"{frontendBaseUrl}/invite/{invitation.Token}",
            ExpiresAt = invitation.ExpiresAt
        };
    }

    public async Task<IEnumerable<InvitationResponse>> GetProjectInvitationsAsync(Guid projectId, Guid requestingUserId)
    {
        await RequireAdminAsync(projectId, requestingUserId);

        var invitations = await _db.GetRepo<ProjectInvitation>().Query()
            .Where(i => i.ProjectId == projectId && i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var result = new List<InvitationResponse>();
        foreach (var inv in invitations)
            result.Add(await BuildResponseAsync(inv));
        return result;
    }

    public async Task CancelInvitationAsync(Guid projectId, Guid invitationId, Guid requestingUserId)
    {
        await RequireAdminAsync(projectId, requestingUserId);

        var invitation = await _db.GetRepo<ProjectInvitation>().GetByIdAsync(invitationId)
            ?? throw new NotFoundException("Invitation not found.");
        if (invitation.ProjectId != projectId) throw new NotFoundException("Invitation not found.");

        _db.GetRepo<ProjectInvitation>().Delete(invitation);
        await _db.SaveAsync();
    }

    public async Task<IEnumerable<InvitationResponse>> GetMyInvitationsAsync(Guid userId)
    {
        var invitations = await _db.GetRepo<ProjectInvitation>().Query()
            .Where(i => i.InvitedUserId == userId && i.Status == InvitationStatus.Pending && i.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();

        var result = new List<InvitationResponse>();
        foreach (var inv in invitations)
            result.Add(await BuildResponseAsync(inv));
        return result;
    }

    public async Task<InvitationResponse> GetInvitationByTokenAsync(string token)
    {
        var invitation = (await _db.GetRepo<ProjectInvitation>().FindAsync(i => i.Token == token))
            .FirstOrDefault() ?? throw new NotFoundException("Invitation not found or already used.");

        if (invitation.ExpiresAt < DateTime.UtcNow) throw new ValidationException("This invitation has expired.");
        if (invitation.Status != InvitationStatus.Pending) throw new ValidationException("This invitation has already been used.");

        return await BuildResponseAsync(invitation);
    }

    public async Task AcceptInvitationAsync(string token, Guid userId)
    {
        var invitation = (await _db.GetRepo<ProjectInvitation>().FindAsync(i => i.Token == token))
            .FirstOrDefault() ?? throw new NotFoundException("Invitation not found.");

        ValidateUsable(invitation);

        if (!invitation.IsLinkInvitation && invitation.InvitedUserId != userId)
            throw new ForbiddenException("This invitation was not sent to you.");

        var alreadyMember = await _db.GetRepo<ProjectMember>()
            .AnyAsync(m => m.ProjectId == invitation.ProjectId && m.UserId == userId);
        if (alreadyMember) throw new ConflictException("You are already a member of this project.");

        var member = new ProjectMember
        {
            UserId = userId,
            ProjectId = invitation.ProjectId,
            Role = invitation.Role ?? ProjectRole.Developer,
            IsPendingRoleAssignment = invitation.IsLinkInvitation
        };

        await _db.GetRepo<ProjectMember>().AddAsync(member);
        invitation.Status = InvitationStatus.Accepted;
        _db.GetRepo<ProjectInvitation>().Update(invitation);
        await _db.SaveAsync();
    }

    public async Task DeclineInvitationAsync(string token, Guid userId)
    {
        var invitation = (await _db.GetRepo<ProjectInvitation>().FindAsync(i => i.Token == token))
            .FirstOrDefault() ?? throw new NotFoundException("Invitation not found.");

        ValidateUsable(invitation);

        if (!invitation.IsLinkInvitation && invitation.InvitedUserId != userId)
            throw new ForbiddenException("This invitation was not sent to you.");

        invitation.Status = InvitationStatus.Declined;
        _db.GetRepo<ProjectInvitation>().Update(invitation);
        await _db.SaveAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task RequireAdminAsync(Guid projectId, Guid userId)
    {
        var member = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");
        if (member.Role != ProjectRole.Admin)
            throw new ForbiddenException("Only Admins can manage invitations.");
    }

    private static void ValidateUsable(ProjectInvitation invitation)
    {
        if (invitation.ExpiresAt < DateTime.UtcNow) throw new ValidationException("This invitation has expired.");
        if (invitation.Status != InvitationStatus.Pending) throw new ValidationException("This invitation has already been used.");
    }

    private async Task<InvitationResponse> BuildResponseAsync(ProjectInvitation inv)
    {
        var project = await _db.GetRepo<Project>().GetByIdAsync(inv.ProjectId);
        var invitedBy = await _db.GetRepo<User>().GetByIdAsync(inv.InvitedById);
        User? invitedUser = inv.InvitedUserId.HasValue ? await _db.GetRepo<User>().GetByIdAsync(inv.InvitedUserId.Value) : null;

        return new InvitationResponse
        {
            Id = inv.Id,
            ProjectId = inv.ProjectId,
            ProjectName = project?.Name ?? "",
            InvitedByUsername = invitedBy?.Username ?? "",
            InvitedUserEmail = invitedUser?.Email,
            InvitedUsername = invitedUser?.Username,
            Role = inv.Role,
            Token = inv.Token,
            Status = inv.Status,
            IsLinkInvitation = inv.IsLinkInvitation,
            CreatedAt = inv.CreatedAt,
            ExpiresAt = inv.ExpiresAt
        };
    }
}
