using TaskManagement.Domain.Models.Tasks;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface ITaskService
{
    Task<TaskResponse> CreateTaskAsync(Guid projectId, CreateTaskRequest request, Guid userId, string? imageUrl = null);
    Task<IEnumerable<TaskResponse>> GetProjectTasksAsync(Guid projectId, Guid userId, string? status = null, string? difficulty = null, string? sortBy = null);
    Task<TaskResponse> GetTaskByIdAsync(Guid projectId, Guid taskId, Guid userId);
    Task<TaskResponse> UpdateTaskStatusAsync(Guid projectId, Guid taskId, UpdateTaskStatusRequest request, Guid userId);
    Task<TaskResponse> DelegateTaskAsync(Guid projectId, Guid taskId, DelegateTaskRequest request, Guid managerId);
    Task<TaskResponse> ReassignTaskAsync(Guid projectId, Guid taskId, ReassignTaskRequest request, Guid userId);
    Task DeleteTaskAsync(Guid projectId, Guid taskId, Guid userId);
    Task<IEnumerable<TaskResponse>> GetMyAssignedTasksAsync(Guid userId);
    Task<TaskResponse> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, Guid userId);
}
