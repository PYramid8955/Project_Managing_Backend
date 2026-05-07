using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Models.Projects;

namespace TaskManagement.BusinessLayer.Core;

public class ProjectHistoryService : IProjectHistoryService
{
    private readonly DbSession _db;

    public ProjectHistoryService(DbSession db) { _db = db; }

    public async Task LogAsync(Guid projectId, string eventType, string description, Guid? actorId = null, Guid? targetUserId = null)
    {
        var ev = new ProjectEvent
        {
            ProjectId = projectId,
            EventType = eventType,
            Description = description,
            ActorId = actorId,
            TargetUserId = targetUserId
        };
        await _db.GetRepo<ProjectEvent>().AddAsync(ev);
        await _db.SaveAsync();
    }

    public async Task<IEnumerable<ProjectHistoryDayResponse>> GetHistoryAsync(Guid projectId, Guid requestingUserId)
    {
        var member = (await _db.GetRepo<ProjectMember>().FindAsync(m => m.ProjectId == projectId && m.UserId == requestingUserId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        var events = await _db.GetRepo<ProjectEvent>().Query()
            .Where(e => e.ProjectId == projectId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        var userIds = events
            .SelectMany(e => new[] { e.ActorId, e.TargetUserId })
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();

        var users = userIds.Count > 0
            ? await _db.GetRepo<User>().Query()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username)
            : new Dictionary<Guid, string>();

        return events
            .GroupBy(e => e.CreatedAt.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new ProjectHistoryDayResponse
            {
                Date = g.Key,
                Events = g.OrderByDescending(e => e.CreatedAt).Select(e => new ProjectEventResponse
                {
                    Id = e.Id,
                    EventType = e.EventType,
                    Description = e.Description,
                    ActorUsername = e.ActorId.HasValue && users.TryGetValue(e.ActorId.Value, out var a) ? a : null,
                    TargetUsername = e.TargetUserId.HasValue && users.TryGetValue(e.TargetUserId.Value, out var t) ? t : null,
                    CreatedAt = e.CreatedAt
                }).ToList(),
                Stats = new ProjectDayStats
                {
                    MembersJoined = g.Count(e => e.EventType == ProjectEventType.MemberJoined),
                    MembersLeft = g.Count(e => e.EventType == ProjectEventType.MemberLeft || e.EventType == ProjectEventType.MemberRemoved),
                    RolesChanged = g.Count(e => e.EventType == ProjectEventType.RoleChanged || e.EventType == ProjectEventType.RoleAssigned),
                    InviteLinksCreated = g.Count(e => e.EventType == ProjectEventType.InviteLinkCreated),
                    InviteLinksDeleted = g.Count(e => e.EventType == ProjectEventType.InviteLinkDeleted),
                    ProjectUpdated = g.Any(e => e.EventType == ProjectEventType.ProjectUpdated)
                }
            });
    }
}
