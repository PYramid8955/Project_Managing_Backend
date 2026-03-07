using Microsoft.AspNetCore.Http;
using TaskManagement.Domain.Models.Submissions;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface ISubmissionService
{
    Task<SubmissionResponse> CreateSubmissionAsync(Guid taskId, IFormFile? file, string? comment, Guid userId, string wwwrootPath);
    Task<IEnumerable<SubmissionResponse>> GetTaskSubmissionsAsync(Guid taskId, Guid userId);
}
