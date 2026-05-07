using Microsoft.AspNetCore.Http;
using TaskManagement.Domain.Models.Projects;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IProjectService
{
    Task<ProjectResponse> CreateProjectAsync(CreateProjectRequest request, Guid userId);
    Task<IEnumerable<ProjectResponse>> GetUserProjectsAsync(Guid userId);
    Task<ProjectResponse> GetProjectByIdAsync(Guid projectId, Guid requestingUserId);
    Task InviteMemberAsync(Guid projectId, InviteMemberRequest request, Guid invitingUserId);
    Task RemoveMemberAsync(Guid projectId, Guid targetUserId, Guid requestingUserId);
    Task<IEnumerable<MemberResponse>> GetMembersAsync(Guid projectId, Guid requestingUserId);
    Task UpdateMemberRoleAsync(Guid projectId, Guid targetUserId, UpdateMemberRoleRequest request, Guid requestingUserId);
    Task AssignDeveloperToManagerAsync(Guid projectId, Guid developerUserId, AssignManagerRequest request, Guid requestingUserId);
    Task<ProjectResponse> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, Guid userId);
    Task<string> UploadProjectImageAsync(Guid projectId, IFormFile file, Guid userId, string webRootPath);
    Task DeleteProjectAsync(Guid projectId, Guid userId);
}
