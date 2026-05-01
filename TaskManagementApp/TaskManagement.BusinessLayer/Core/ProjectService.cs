using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Projects;

namespace TaskManagement.BusinessLayer.Core;

public class ProjectService : IProjectService
{
    private readonly DbSession _db;
    private readonly INotificationService _notifications;
    private readonly IProjectHistoryService _history;

    public ProjectService(DbSession db, INotificationService notifications, IProjectHistoryService history)
    {
        _db = db;
        _notifications = notifications;
        _history = history;
    }

    public async Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, Guid userId)
    {
        var project = new Project { Name = request.Name, Description = request.Description, CreatedById = userId };
        await _db.GetRepo<Project>().AddAsync(project);
        await _db.GetRepo<ProjectMember>().AddAsync(new ProjectMember { UserId = userId, ProjectId = project.Id, Role = ProjectRole.Admin });
        await _db.SaveAsync();

        var creator = await _db.GetRepo<User>().GetByIdAsync(userId);
        await _history.LogAsync(project.Id, ProjectEventType.ProjectCreated,
            $"Project \"{project.Name}\" was created.", userId);

        return BuildProjectResponse(project, creator?.Username ?? "", 1);
    }

    public async Task<IEnumerable<ProjectResponse>> GetUserProjectsAsync(Guid userId)
    {
        var memberships = await _db.GetRepo<ProjectMember>().Query()
            .Where(m => m.UserId == userId).Select(m => m.ProjectId).ToListAsync();

        var projects = await _db.GetRepo<Project>().Query()
            .Where(p => memberships.Contains(p.Id)).ToListAsync();

        if (projects.Count == 0) return Enumerable.Empty<ProjectResponse>();

        var projectIds = projects.Select(p => p.Id).ToList();

        var memberCounts = await _db.GetRepo<ProjectMember>().Query()
            .Where(m => projectIds.Contains(m.ProjectId))
            .GroupBy(m => m.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ProjectId, x => x.Count);

        var creatorIds = projects.Select(p => p.CreatedById).Distinct().ToList();
        var creators = await _db.GetRepo<User>().Query()
            .Where(u => creatorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Username);

        return projects.Select(p => BuildProjectResponse(
            p,
            creators.GetValueOrDefault(p.CreatedById, ""),
            memberCounts.GetValueOrDefault(p.Id, 0)
        ));
    }

    public async Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, Guid requestingUserId)
    {
        await EnsureMemberAsync(projectId, requestingUserId);
        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId) ?? throw new NotFoundException("Project not found.");
        var count = await _db.GetRepo<ProjectMember>().CountAsync(m => m.ProjectId == projectId);
        var creator = await _db.GetRepo<User>().GetByIdAsync(project.CreatedById);
        return BuildProjectResponse(project, creator?.Username ?? "", count);
    }

    public async Task InviteMemberAsync(Guid projectId, InviteMemberRequest request, Guid invitingUserId)
    {
        var role = await GetMemberRoleAsync(projectId, invitingUserId)
            ?? throw new ForbiddenException("You are not a member of this project.");
        if (role != ProjectRole.Admin)
            throw new ForbiddenException("Only project Admins can invite members.");

        var users = await _db.GetRepo<User>().FindAsync(u => u.Email == request.Email.ToLower());
        var targetUser = users.FirstOrDefault() ?? throw new NotFoundException($"No user found with email '{request.Email}'.");

        var alreadyMember = await _db.GetRepo<ProjectMember>().AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.Id);
        if (alreadyMember) throw new ConflictException("This user is already a member of the project.");

        await _db.GetRepo<ProjectMember>().AddAsync(new ProjectMember { UserId = targetUser.Id, ProjectId = projectId, Role = request.Role });
        await _db.SaveAsync();
    }

    public async Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid requestingUserId)
    {
        var requesterRole = await GetMemberRoleAsync(projectId, requestingUserId)
            ?? throw new ForbiddenException("You are not a member of this project.");
        if (requesterRole != ProjectRole.Admin) throw new ForbiddenException("Only project Admins can remove members.");
        if (targetUserId == requestingUserId) throw new ValidationException("You cannot remove yourself.");

        var memberRepo = _db.GetRepo<ProjectMember>();
        var members = await memberRepo.FindAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        var member = members.FirstOrDefault() ?? throw new NotFoundException("Member not found in this project.");

        if (member.Role == ProjectRole.Admin)
        {
            var adminCount = await memberRepo.CountAsync(m => m.ProjectId == projectId && m.Role == ProjectRole.Admin);
            if (adminCount <= 1) throw new ValidationException("Cannot remove the last Admin of a project.");
        }

        if (member.Role == ProjectRole.Manager)
            await ClearManagerGroupAsync(projectId, targetUserId);

        if (member.Role == ProjectRole.Developer)
        {
            member.ManagerUserId = null;
            memberRepo.Update(member);
        }

        memberRepo.Delete(member);
        await _db.SaveAsync();

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId);
        var removedUser = await _db.GetRepo<User>().GetByIdAsync(targetUserId);
        var requester = await _db.GetRepo<User>().GetByIdAsync(requestingUserId);

        await _notifications.CreateAsync(
            targetUserId,
            NotificationType.RemovedFromProject,
            "Removed from project",
            $"You have been removed from the project \"{project?.Name}\".",
            projectId,
            "Project"
        );

        await _history.LogAsync(projectId, ProjectEventType.MemberRemoved,
            $"{requester?.Username ?? "Admin"} removed {removedUser?.Username ?? "a member"} from the project.",
            requestingUserId, targetUserId);
    }

    public async Task<IEnumerable<MemberResponse>> GetMembersAsync(Guid projectId, Guid requestingUserId)
    {
        await EnsureMemberAsync(projectId, requestingUserId);

        var members = await _db.GetRepo<ProjectMember>().FindAsync(m => m.ProjectId == projectId);

        var userIds = members.Select(m => m.UserId)
            .Concat(members.Where(m => m.ManagerUserId.HasValue).Select(m => m.ManagerUserId!.Value))
            .Distinct().ToList();

        var users = await _db.GetRepo<User>().Query()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var result = new List<MemberResponse>();
        foreach (var m in members)
        {
            if (!users.TryGetValue(m.UserId, out var user)) continue;
            string? managerUsername = m.ManagerUserId.HasValue && users.TryGetValue(m.ManagerUserId.Value, out var mgr)
                ? mgr.Username : null;

            result.Add(new MemberResponse
            {
                UserId = m.UserId,
                Username = user.Username,
                Email = user.Email,
                Role = m.Role,
                JoinedAt = m.JoinedAt,
                ManagerUserId = m.ManagerUserId,
                ManagerUsername = managerUsername,
                IsPendingRoleAssignment = m.IsPendingRoleAssignment
            });
        }
        return result;
    }

    public async Task UpdateMemberRoleAsync(Guid projectId, Guid targetUserId, UpdateMemberRoleRequest request, Guid requestingUserId)
    {
        var requesterRole = await GetMemberRoleAsync(projectId, requestingUserId)
            ?? throw new ForbiddenException("You are not a member of this project.");
        if (requesterRole != ProjectRole.Admin) throw new ForbiddenException("Only project Admins can change member roles.");
        if (targetUserId == requestingUserId) throw new ValidationException("You cannot change your own role.");

        var memberRepo = _db.GetRepo<ProjectMember>();
        var members = await memberRepo.FindAsync(m => m.ProjectId == projectId && m.UserId == targetUserId);
        var member = members.FirstOrDefault() ?? throw new NotFoundException("Member not found in this project.");

        if (member.Role == ProjectRole.Admin && request.NewRole != ProjectRole.Admin)
        {
            var adminCount = await memberRepo.CountAsync(m => m.ProjectId == projectId && m.Role == ProjectRole.Admin);
            if (adminCount <= 1) throw new ValidationException("Cannot change the role of the last Admin.");
        }

        var oldRole = member.Role;
        member.Role = request.NewRole;
        member.IsPendingRoleAssignment = false;

        if (oldRole == ProjectRole.Manager && request.NewRole != ProjectRole.Manager)
            await ClearManagerGroupAsync(projectId, targetUserId);

        if (oldRole == ProjectRole.Developer && request.NewRole != ProjectRole.Developer)
            member.ManagerUserId = null;

        if (request.NewRole == ProjectRole.Admin || request.NewRole == ProjectRole.Manager)
            member.ManagerUserId = null;

        memberRepo.Update(member);
        await _db.SaveAsync();

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId);
        var targetUser = await _db.GetRepo<User>().GetByIdAsync(targetUserId);
        var requester = await _db.GetRepo<User>().GetByIdAsync(requestingUserId);
        var isFirstAssignment = oldRole == ProjectRole.Unassigned;

        await _notifications.CreateAsync(
            targetUserId,
            isFirstAssignment ? NotificationType.RoleAssigned : NotificationType.RoleChanged,
            isFirstAssignment ? "Role assigned" : "Role changed",
            isFirstAssignment
                ? $"You have been assigned the role of {request.NewRole} in \"{project?.Name}\"."
                : $"Your role in \"{project?.Name}\" has been changed from {oldRole} to {request.NewRole}.",
            projectId,
            "Project"
        );

        await _history.LogAsync(projectId,
            isFirstAssignment ? ProjectEventType.RoleAssigned : ProjectEventType.RoleChanged,
            isFirstAssignment
                ? $"{requester?.Username ?? "Admin"} assigned {targetUser?.Username} the role {request.NewRole}."
                : $"{requester?.Username ?? "Admin"} changed {targetUser?.Username}'s role from {oldRole} to {request.NewRole}.",
            requestingUserId, targetUserId);
    }

    public async Task AssignDeveloperToManagerAsync(Guid projectId, Guid developerUserId, AssignManagerRequest request, Guid requestingUserId)
    {
        var requesterRole = await GetMemberRoleAsync(projectId, requestingUserId)
            ?? throw new ForbiddenException("You are not a member of this project.");
        if (requesterRole != ProjectRole.Admin) throw new ForbiddenException("Only Admins can assign developers to managers.");

        var memberRepo = _db.GetRepo<ProjectMember>();
        var devMembers = await memberRepo.FindAsync(m => m.ProjectId == projectId && m.UserId == developerUserId);
        var devMember = devMembers.FirstOrDefault() ?? throw new NotFoundException("Developer not found in this project.");

        if (devMember.Role != ProjectRole.Developer)
            throw new ValidationException("Only Developers can be assigned to a Manager group.");

        if (request.ManagerUserId.HasValue)
        {
            var mgrMembers = await memberRepo.FindAsync(m => m.ProjectId == projectId && m.UserId == request.ManagerUserId.Value);
            var mgrMember = mgrMembers.FirstOrDefault() ?? throw new NotFoundException("Manager not found in this project.");
            if (mgrMember.Role != ProjectRole.Manager)
                throw new ValidationException("The specified user is not a Manager in this project.");
        }

        devMember.ManagerUserId = request.ManagerUserId;
        memberRepo.Update(devMember);
        await _db.SaveAsync();
    }

    public async Task<ProjectResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (membership.Role != ProjectRole.Admin)
            throw new ForbiddenException("Only Admins can update project details.");

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId)
            ?? throw new NotFoundException("Project not found.");

        if (request.Name != null) project.Name = request.Name;
        if (request.Description != null) project.Description = request.Description;

        _db.GetRepo<Project>().Update(project);
        await _db.SaveAsync();

        var updater = await _db.GetRepo<User>().GetByIdAsync(userId);
        await _history.LogAsync(projectId, ProjectEventType.ProjectUpdated,
            $"{updater?.Username ?? "Admin"} updated the project details.", userId);

        var creator = await _db.GetRepo<User>().GetByIdAsync(project.CreatedById);
        var memberCount = await _db.GetRepo<ProjectMember>().CountAsync(m => m.ProjectId == projectId);
        return BuildProjectResponse(project, creator?.Username ?? "", memberCount);
    }

    public async Task<string> UploadProjectImageAsync(Guid projectId, IFormFile file, Guid userId, string webRootPath)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");
        if (membership.Role != ProjectRole.Admin)
            throw new ForbiddenException("Only Admins can update the project image.");

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId)
            ?? throw new NotFoundException("Project not found.");

        var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExts.Contains(ext))
            throw new ValidationException("Only image files are allowed (png, jpg, jpeg, gif, webp).");
        if (file.Length > 5 * 1024 * 1024)
            throw new ValidationException("Image must be under 5MB.");

        if (!string.IsNullOrEmpty(project.ImageUrl))
        {
            var oldPath = Path.Combine(webRootPath, project.ImageUrl.TrimStart('/'));
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var imgDir = Path.Combine(webRootPath, "uploads", "projects");
        Directory.CreateDirectory(imgDir);
        var fileName = $"{projectId}{ext}";
        var fullPath = Path.Combine(imgDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/projects/{fileName}";
        project.ImageUrl = url;
        _db.GetRepo<Project>().Update(project);
        await _db.SaveAsync();

        return url;
    }

    public async Task DeleteProjectAsync(Guid projectId, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (membership.Role != ProjectRole.Admin)
            throw new ForbiddenException("Only Admins can delete a project.");

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId)
            ?? throw new NotFoundException("Project not found.");

        if (project.CreatedById != userId)
            throw new ForbiddenException("Only the project creator can delete it.");

        _db.GetRepo<Project>().Delete(project);
        await _db.SaveAsync();
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task ClearManagerGroupAsync(Guid projectId, Guid managerUserId)
    {
        var memberRepo = _db.GetRepo<ProjectMember>();
        var groupMembers = await memberRepo.Query()
            .Where(m => m.ProjectId == projectId && m.ManagerUserId == managerUserId)
            .ToListAsync();

        foreach (var gm in groupMembers)
        {
            gm.ManagerUserId = null;
            memberRepo.Update(gm);
        }
    }

    private async Task<ProjectRole?> GetMemberRoleAsync(Guid projectId, Guid userId)
    {
        var members = await _db.GetRepo<ProjectMember>().FindAsync(m => m.ProjectId == projectId && m.UserId == userId);
        return members.FirstOrDefault()?.Role;
    }

    private async Task EnsureMemberAsync(Guid projectId, Guid userId)
    {
        var members = await _db.GetRepo<ProjectMember>().FindAsync(m => m.ProjectId == projectId && m.UserId == userId);
        var member = members.FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");
        if (member.IsPendingRoleAssignment) throw new ForbiddenException("Your role has not been assigned yet. Contact the project Admin.");
    }

    private static ProjectResponse BuildProjectResponse(Project p, string creatorUsername, int memberCount) =>
        new() { Id = p.Id, Name = p.Name, Description = p.Description, CreatedById = p.CreatedById, CreatedByUsername = creatorUsername, CreatedAt = p.CreatedAt, MemberCount = memberCount, ImageUrl = p.ImageUrl };
}
