using Microsoft.AspNetCore.Http;
using TaskManagement.Domain.Models.Auth;
using TaskManagement.Domain.Models.Projects;
using TaskManagement.Domain.Models.Users;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IUserService
{
    Task<PublicUserProfileResponse> GetPublicProfileAsync(Guid targetUserId, Guid requestingUserId);
    Task<string> UploadAvatarAsync(Guid userId, IFormFile file, string webRootPath);
    Task DeleteAccountAsync(Guid userId);
    Task<AuthResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request);
    Task<IEnumerable<ProjectResponse>> GetAdminOnlyProjectsAsync(Guid userId);
}
