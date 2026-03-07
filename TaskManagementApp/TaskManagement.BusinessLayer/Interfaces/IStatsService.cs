using TaskManagement.Domain.Models.Stats;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IStatsService
{
    Task<ProjectStatsResponse> GetProjectStatsAsync(Guid projectId, Guid userId);
    Task<ProjectRankingResponse> GetProjectRankingAsync(Guid projectId, Guid userId);
}
