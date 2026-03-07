using TaskManagement.Domain.Models.Tasks;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface ICommentService
{
    Task<TaskCommentResponse> AddCommentAsync(Guid projectId, Guid taskId, TaskCommentRequest request, Guid userId);
    Task<IEnumerable<TaskCommentResponse>> GetTaskCommentsAsync(Guid projectId, Guid taskId, Guid userId);
}
