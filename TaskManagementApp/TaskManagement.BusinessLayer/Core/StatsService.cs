using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Stats;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.BusinessLayer.Core;

public class StatsService : IStatsService
{
    private readonly DbSession _db;

    public StatsService(DbSession db)
    {
        _db = db;
    }

    public async Task<ProjectStatsResponse> GetProjectStatsAsync(Guid projectId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);

        var project = await _db.GetRepo<Project>().GetByIdAsync(projectId) ?? throw new NotFoundException("Project not found.");
        var tasks = await _db.GetRepo<AppTask>().Query().Where(t => t.ProjectId == projectId).ToListAsync();

        var total = tasks.Count;
        var approved = tasks.Count(t => t.Status == TaskStatus.Approved);
        var pending = tasks.Count(t => t.Status == TaskStatus.Submitted || t.Status == TaskStatus.InProgress);
        var rejected = tasks.Count(t => t.Status == TaskStatus.Rejected);
        var totalPoints = tasks.Where(t => t.Status == TaskStatus.Approved).Sum(t => (int)t.Difficulty);
        var easy = tasks.Count(t => t.Difficulty == TaskDifficulty.Easy);
        var medium = tasks.Count(t => t.Difficulty == TaskDifficulty.Medium);
        var hard = tasks.Count(t => t.Difficulty == TaskDifficulty.Hard);
        var overdue = tasks.Count(t => t.DueDate.HasValue && t.DueDate.Value < DateTime.UtcNow && t.Status != TaskStatus.Approved);

        return new ProjectStatsResponse
        {
            ProjectId = projectId,
            ProjectName = project.Name,
            TotalTasks = total,
            ApprovedTasks = approved,
            PendingTasks = pending,
            RejectedTasks = rejected,
            ApprovalRate = total > 0 ? Math.Round((double)approved / total * 100, 1) : 0,
            TotalPoints = totalPoints,
            EasyTasks = easy,
            MediumTasks = medium,
            HardTasks = hard,
            OverdueTasks = overdue
        };
    }

    public async Task<ProjectRankingResponse> GetProjectRankingAsync(Guid projectId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);

        var approvedTasks = await _db.GetRepo<AppTask>().Query()
            .Where(t => t.ProjectId == projectId && t.Status == TaskStatus.Approved && t.AssignedToId != null)
            .ToListAsync();

        var allAssignedTasks = await _db.GetRepo<AppTask>().Query()
            .Where(t => t.ProjectId == projectId && t.AssignedToId != null)
            .ToListAsync();

        var members = await _db.GetRepo<ProjectMember>().Query()
            .Where(m => m.ProjectId == projectId)
            .ToListAsync();

        var userRepo = _db.GetRepo<User>();

        var devRanking = new List<RankingEntry>();
        var mgrRanking = new List<RankingEntry>();

        foreach (var member in members)
        {
            // Admins excluded from ranking
            if (member.Role == ProjectRole.Admin) continue;

            var user = await userRepo.GetByIdAsync(member.UserId);
            if (user == null) continue;

            var userApproved = approvedTasks.Where(t => t.AssignedToId == member.UserId).ToList();
            var userTotal = allAssignedTasks.Count(t => t.AssignedToId == member.UserId);
            var personalPoints = userApproved.Sum(t => (int)t.Difficulty);

            if (member.Role == ProjectRole.Developer)
            {
                devRanking.Add(new RankingEntry
                {
                    UserId = member.UserId,
                    Username = user.Username,
                    Role = "Developer",
                    PersonalPoints = personalPoints,
                    GroupPoints = 0,
                    Points = personalPoints,
                    ApprovedTasks = userApproved.Count,
                    TotalSubmissions = userTotal,
                    ApprovalRate = userTotal > 0 ? Math.Round((double)userApproved.Count / userTotal * 100, 1) : 0
                });
            }
            else if (member.Role == ProjectRole.Manager)
            {
                // Group points = sum of all approved tasks by developers in this manager's group
                var groupDevIds = members
                    .Where(m2 => m2.ManagerUserId == member.UserId && m2.Role == ProjectRole.Developer)
                    .Select(m2 => m2.UserId)
                    .ToHashSet();

                var groupApproved = approvedTasks.Where(t => t.AssignedToId.HasValue && groupDevIds.Contains(t.AssignedToId.Value)).ToList();
                var groupPoints = groupApproved.Sum(t => (int)t.Difficulty);

                mgrRanking.Add(new RankingEntry
                {
                    UserId = member.UserId,
                    Username = user.Username,
                    Role = "Manager",
                    PersonalPoints = personalPoints,
                    GroupPoints = groupPoints,
                    Points = personalPoints + groupPoints,
                    ApprovedTasks = userApproved.Count,
                    TotalSubmissions = userTotal,
                    ApprovalRate = userTotal > 0 ? Math.Round((double)userApproved.Count / userTotal * 100, 1) : 0
                });
            }
        }

        // Sort and assign ranks
        var sortedDevs = devRanking.OrderByDescending(r => r.Points).ToList();
        for (int i = 0; i < sortedDevs.Count; i++) sortedDevs[i].Rank = i + 1;

        var sortedMgrs = mgrRanking.OrderByDescending(r => r.Points).ToList();
        for (int i = 0; i < sortedMgrs.Count; i++) sortedMgrs[i].Rank = i + 1;

        return new ProjectRankingResponse
        {
            DeveloperRanking = sortedDevs,
            ManagerRanking = sortedMgrs
        };
    }

    private async Task EnsureMemberAsync(Guid projectId, Guid userId)
    {
        var isMember = await _db.GetRepo<ProjectMember>().AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (!isMember) throw new ForbiddenException("You are not a member of this project.");
    }
}
