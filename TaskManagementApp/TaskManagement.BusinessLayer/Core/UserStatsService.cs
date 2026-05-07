using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Stats;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;


namespace TaskManagement.BusinessLayer.Core;

public class UserStatsService : IUserStatsService
{
    private readonly DbSession _db;

    public UserStatsService(DbSession db)
    {
        _db = db;
    }

    public async Task<UserStatsResponse> GetMyStatsAsync(Guid userId)
    {
        return await BuildUserStatsAsync(userId);
    }

    public async Task<UserStatsResponse> GetMemberStatsAsync(Guid projectId, Guid targetUserId, Guid requesterId)
    {
        // Requester must be Admin or Manager in the project
        var requesterMembership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == requesterId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (requesterMembership.Role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can view member stats.");

        // Target must be a member of the project
        var targetMembership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == targetUserId))
            .FirstOrDefault() ?? throw new NotFoundException("Member not found in this project.");

        return await BuildUserStatsAsync(targetUserId);
    }

    private async Task<UserStatsResponse> BuildUserStatsAsync(Guid userId)
    {
        var user = await _db.GetRepo<User>().GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var memberships = await _db.GetRepo<ProjectMember>()
            .Query()
            .Where(m => m.UserId == userId)
            .ToListAsync();

        var allTasks = await _db.GetRepo<AppTask>()
            .Query()
            .Where(t => t.AssignedToId == userId)
            .ToListAsync();

        var approvedTasks = allTasks.Where(t => t.Status == TaskStatus.Approved).ToList();
        var rejectedCount = allTasks.Count(t => t.Status == TaskStatus.Rejected);
        var totalPoints = approvedTasks.Sum(t => (int)t.Difficulty);

        var contributions = new List<ProjectContribution>();

        foreach (var membership in memberships)
        {
            var project = await _db.GetRepo<Project>().GetByIdAsync(membership.ProjectId);
            if (project == null) continue;

            var projectTasks = allTasks.Where(t => t.ProjectId == membership.ProjectId).ToList();
            var projectApproved = projectTasks.Where(t => t.Status == TaskStatus.Approved).ToList();

            contributions.Add(new ProjectContribution
            {
                ProjectId = membership.ProjectId,
                ProjectName = project.Name,
                Role = membership.Role.ToString(),
                AssignedTasks = projectTasks.Count,
                ApprovedTasks = projectApproved.Count,
                Points = projectApproved.Sum(t => (int)t.Difficulty)
            });
        }

        return new UserStatsResponse
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            TotalProjects = memberships.Count,
            TotalAssignedTasks = allTasks.Count,
            ApprovedTasks = approvedTasks.Count,
            RejectedTasks = rejectedCount,
            TotalPoints = totalPoints,
            ApprovalRate = allTasks.Count > 0
                ? Math.Round((double)approvedTasks.Count / allTasks.Count * 100, 1)
                : 0,
            ProjectContributions = contributions
        };
    }

    public async Task<IEnumerable<UserSearchResult>> SearchUsersAsync(string query, Guid requesterId)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length < 2)
            return [];

        var lower = query.Trim().ToLower();
        var users = await _db.GetRepo<User>().Query()
            .Where(u => u.Id != requesterId &&
                (u.Email.ToLower().Contains(lower) || u.Username.ToLower().Contains(lower)))
            .Take(10)
            .Select(u => new UserSearchResult { UserId = u.Id, Username = u.Username, Email = u.Email })
            .ToListAsync();

        return users;
    }
}
