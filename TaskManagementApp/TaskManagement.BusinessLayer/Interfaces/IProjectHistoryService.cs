using TaskManagement.Domain.Models.Projects;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IProjectHistoryService
{
    Task LogAsync(Guid projectId, string eventType, string description, Guid? actorId = null, Guid? targetUserId = null);
    Task<IEnumerable<ProjectHistoryDayResponse>> GetHistoryAsync(Guid projectId, Guid requestingUserId);
}
