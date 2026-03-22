using TaskManagement.Domain.Models.Stats;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IUserStatsService
{
    Task<UserStatsResponse> GetMyStatsAsync(Guid userId);
    Task<UserStatsResponse> GetMemberStatsAsync(Guid projectId, Guid targetUserId, Guid requesterId);
    Task<IEnumerable<UserSearchResult>> SearchUsersAsync(string query, Guid requesterId);
}
