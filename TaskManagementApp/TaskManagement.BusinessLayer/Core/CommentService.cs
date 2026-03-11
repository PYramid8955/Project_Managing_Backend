using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Models.Tasks;

namespace TaskManagement.BusinessLayer.Core;

public class CommentService : ICommentService
{
    private readonly DbSession _db;

    public CommentService(DbSession db)
    {
        _db = db;
    }

    public async Task<TaskCommentResponse> AddCommentAsync(Guid projectId, Guid taskId, TaskCommentRequest request, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId)
            ?? throw new NotFoundException("Task not found.");

        if (task.ProjectId != projectId)
            throw new NotFoundException("Task not found in this project.");

        var comment = new TaskComment
        {
            TaskId = taskId,
            UserId = userId,
            Content = request.Content
        };

        await _db.GetRepo<TaskComment>().AddAsync(comment);
        await _db.SaveAsync();

        var user = await _db.GetRepo<User>().GetByIdAsync(userId);

        return new TaskCommentResponse
        {
            Id = comment.Id,
            TaskId = comment.TaskId,
            UserId = comment.UserId,
            Username = user?.Username ?? "",
            Content = comment.Content,
            CreatedAt = comment.CreatedAt
        };
    }

    public async Task<IEnumerable<TaskCommentResponse>> GetTaskCommentsAsync(Guid projectId, Guid taskId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId)
            ?? throw new NotFoundException("Task not found.");

        if (task.ProjectId != projectId)
            throw new NotFoundException("Task not found in this project.");

        var comments = await _db.GetRepo<TaskComment>()
            .Query()
            .Where(c => c.TaskId == taskId)
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        var userRepo = _db.GetRepo<User>();
        var result = new List<TaskCommentResponse>();

        foreach (var c in comments)
        {
            var user = await userRepo.GetByIdAsync(c.UserId);
            result.Add(new TaskCommentResponse
            {
                Id = c.Id,
                TaskId = c.TaskId,
                UserId = c.UserId,
                Username = user?.Username ?? "",
                Content = c.Content,
                CreatedAt = c.CreatedAt
            });
        }

        return result;
    }

    private async Task EnsureMemberAsync(Guid projectId, Guid userId)
    {
        var isMember = await _db.GetRepo<ProjectMember>()
            .AnyAsync(m => m.ProjectId == projectId && m.UserId == userId);
        if (!isMember)
            throw new ForbiddenException("You are not a member of this project.");
    }
}
