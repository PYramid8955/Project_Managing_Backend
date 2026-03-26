using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Tasks;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.BusinessLayer.Core;

public class TaskService : ITaskService
{
    private readonly DbSession _db;

    public TaskService(DbSession db)
    {
        _db = db;
    }

    public async Task<TaskResponse> CreateTaskAsync(Guid projectId, CreateTaskRequest request, Guid userId, string? imageUrl = null)
    {
        var requesterMembership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        var role = requesterMembership.Role;

        if (role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can create tasks.");

        // ── Validate assignee ──────────────────────────────────────────────────
        if (request.AssignedToId.HasValue)
        {
            var assigneeMembership = (await _db.GetRepo<ProjectMember>()
                .FindAsync(m => m.ProjectId == projectId && m.UserId == request.AssignedToId.Value))
                .FirstOrDefault();

            if (assigneeMembership == null)
                throw new ValidationException("The assigned user is not a member of this project.");

            if (assigneeMembership.Role == ProjectRole.Admin)
                throw new ValidationException("Tasks cannot be assigned to Admins.");

            if (role == ProjectRole.Manager)
            {
                // Managers can assign to themselves (Direct mode)
                if (request.AssignedToId.Value != userId)
                {
                    // Or to developers in their group
                    if (assigneeMembership.Role != ProjectRole.Developer)
                        throw new ValidationException("Managers can only assign tasks to Developers in their group or to themselves.");

                    if (assigneeMembership.ManagerUserId != userId)
                        throw new ValidationException("You can only assign tasks to developers in your own group.");
                }
                // Managers cannot assign tasks to other Managers
                if (assigneeMembership.Role == ProjectRole.Manager && request.AssignedToId.Value != userId)
                    throw new ValidationException("Managers cannot assign tasks to other Managers.");
            }

            if (role == ProjectRole.Admin && assigneeMembership.Role == ProjectRole.Manager)
            {
                // Admin assigning to Manager: force Direct mode (auto-assign)
                request.AssignmentMode = AssignmentMode.Direct;
            }
        }

        if (request.AssignmentMode == AssignmentMode.Direct && !request.AssignedToId.HasValue)
            throw new ValidationException("Direct assignment mode requires a specific user to be assigned.");

        if (request.DueDate.HasValue && request.DueDate.Value < DateTime.UtcNow)
            throw new ValidationException("Due date cannot be in the past.");

        var task = new AppTask
        {
            Title = request.Title,
            Description = request.Description,
            ProjectId = projectId,
            CreatedById = userId,
            AssignedToId = request.AssignedToId,
            Difficulty = request.Difficulty,
            AssignmentMode = request.AssignmentMode,
            RequiresAttachment = request.RequiresAttachment,
            DueDate = request.DueDate,
            ImageUrl = imageUrl,
            Status = request.AssignmentMode == AssignmentMode.Direct ? TaskStatus.InProgress : TaskStatus.ToDo
        };

        await _db.GetRepo<AppTask>().AddAsync(task);

        if (request.AssignmentMode == AssignmentMode.Invited && request.EligibleUserIds.Count > 0)
        {
            foreach (var eligibleUserId in request.EligibleUserIds.Distinct())
            {
                var isMember = await _db.GetRepo<ProjectMember>()
                    .AnyAsync(m => m.ProjectId == projectId && m.UserId == eligibleUserId);
                if (isMember)
                    await _db.GetRepo<TaskEligibleUser>().AddAsync(new TaskEligibleUser { TaskId = task.Id, UserId = eligibleUserId });
            }
        }

        await _db.SaveAsync();
        return await BuildTaskResponseAsync(task);
    }

    public async Task<IEnumerable<TaskResponse>> GetProjectTasksAsync(Guid projectId, Guid userId, string? status = null, string? difficulty = null, string? sortBy = null)
    {
        await EnsureMemberAsync(projectId, userId);

        var query = _db.GetRepo<AppTask>().Query().Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TaskStatus>(status, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrEmpty(difficulty) && Enum.TryParse<TaskDifficulty>(difficulty, out var parsedDiff))
            query = query.Where(t => t.Difficulty == parsedDiff);

        query = sortBy switch
        {
            "dueDate" => query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate),
            "difficulty" => query.OrderByDescending(t => t.Difficulty),
            "status" => query.OrderBy(t => t.Status),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var tasks = await query.ToListAsync();

        var result = new List<TaskResponse>();
        foreach (var t in tasks)
            result.Add(await BuildTaskResponseAsync(t));
        return result;
    }

    public async Task<TaskResponse> GetTaskByIdAsync(Guid projectId, Guid taskId, Guid userId)
    {
        await EnsureMemberAsync(projectId, userId);
        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");
        return await BuildTaskResponseAsync(task);
    }

    public async Task<TaskResponse> UpdateTaskStatusAsync(Guid projectId, Guid taskId, UpdateTaskStatusRequest request, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        var role = membership.Role;
        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");

        if (role == ProjectRole.Developer)
        {
            // Developer can only accept (ToDo → InProgress)
            if (request.NewStatus == TaskStatus.InProgress && task.Status == TaskStatus.ToDo)
            {
                if (task.AssignmentMode == AssignmentMode.Direct)
                    throw new ForbiddenException("This task is directly assigned and does not require acceptance.");

                if (task.AssignmentMode == AssignmentMode.Invited)
                {
                    var isEligible = await _db.GetRepo<TaskEligibleUser>()
                        .AnyAsync(eu => eu.TaskId == taskId && eu.UserId == userId);
                    if (!isEligible)
                        throw new ForbiddenException("You are not in the list of eligible developers for this task.");
                }

                if (task.AssignedToId != null && task.AssignedToId != userId)
                    throw new ForbiddenException("This task is assigned to another developer.");

                task.Status = TaskStatus.InProgress;
                task.RejectionFeedback = null;
                if (task.AssignedToId == null)
                    task.AssignedToId = userId;
            }
            else
            {
                throw new ForbiddenException("Developers can only accept tasks (ToDo → InProgress).");
            }
        }
        else
        {
            if (request.NewStatus == TaskStatus.Approved || request.NewStatus == TaskStatus.Rejected)
                throw new ValidationException("Use the review endpoint to approve or reject submitted tasks.");
            task.Status = request.NewStatus;
        }

        _db.GetRepo<AppTask>().Update(task);
        await _db.SaveAsync();
        return await BuildTaskResponseAsync(task);
    }

    public async Task<TaskResponse> DelegateTaskAsync(Guid projectId, Guid taskId, DelegateTaskRequest request, Guid managerId)
    {
        var managerMembership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == managerId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (managerMembership.Role != ProjectRole.Manager)
            throw new ForbiddenException("Only Managers can delegate tasks.");

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");
        if (task.AssignedToId != managerId) throw new ForbiddenException("You can only delegate tasks assigned to you.");
        if (task.Status != TaskStatus.InProgress) throw new ValidationException("Only InProgress tasks can be delegated.");

        // Target must be a developer in the manager's group
        var devMembership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == request.DeveloperUserId))
            .FirstOrDefault() ?? throw new NotFoundException("Developer not found in this project.");

        if (devMembership.Role != ProjectRole.Developer)
            throw new ValidationException("Tasks can only be delegated to Developers.");

        if (devMembership.ManagerUserId != managerId)
            throw new ForbiddenException("You can only delegate to developers in your own group.");

        // Reassign task to developer, keep InProgress (delegation is immediate)
        task.AssignedToId = request.DeveloperUserId;
        task.Status = TaskStatus.InProgress;
        task.AssignmentMode = AssignmentMode.Direct;
        task.RejectionFeedback = null;
        _db.GetRepo<AppTask>().Update(task);
        await _db.SaveAsync();

        return await BuildTaskResponseAsync(task);
    }

    public async Task<TaskResponse> ReassignTaskAsync(Guid projectId, Guid taskId, ReassignTaskRequest request, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (membership.Role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can reassign tasks.");

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");

        if (task.Status == TaskStatus.Approved)
            throw new ValidationException("Approved tasks cannot be reassigned.");

        if (request.AssignedToId.HasValue)
        {
            var assigneeMembership = (await _db.GetRepo<ProjectMember>()
                .FindAsync(m => m.ProjectId == projectId && m.UserId == request.AssignedToId.Value))
                .FirstOrDefault() ?? throw new ValidationException("The assigned user is not a member of this project.");

            if (assigneeMembership.Role == ProjectRole.Admin)
                throw new ValidationException("Tasks cannot be assigned to Admins.");

            if (membership.Role == ProjectRole.Manager)
            {
                if (request.AssignedToId.Value != userId)
                {
                    if (assigneeMembership.Role != ProjectRole.Developer || assigneeMembership.ManagerUserId != userId)
                        throw new ForbiddenException("You can only assign tasks to developers in your own group.");
                }
            }

            task.AssignedToId = request.AssignedToId;
            task.AssignmentMode = AssignmentMode.Direct;
            // If task was InProgress by someone else, reset to ToDo
            if (task.Status == TaskStatus.InProgress && task.AssignedToId != request.AssignedToId)
                task.Status = TaskStatus.ToDo;
        }
        else
        {
            // Unassign: open to group
            if (membership.Role == ProjectRole.Manager)
                throw new ValidationException("Managers cannot unassign tasks.");

            task.AssignedToId = null;
            task.AssignmentMode = AssignmentMode.Open;
            if (task.Status == TaskStatus.InProgress)
                task.Status = TaskStatus.ToDo;
        }

        _db.GetRepo<AppTask>().Update(task);
        await _db.SaveAsync();
        return await BuildTaskResponseAsync(task);
    }

    public async Task<IEnumerable<TaskResponse>> GetMyAssignedTasksAsync(Guid userId)
    {
        var tasks = await _db.GetRepo<AppTask>().Query()
            .Where(t => t.AssignedToId == userId && t.Status != TaskStatus.Approved)
            .OrderBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        var result = new List<TaskResponse>();
        foreach (var t in tasks)
            result.Add(await BuildTaskResponseAsync(t));
        return result;
    }

    public async Task DeleteTaskAsync(Guid projectId, Guid taskId, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (membership.Role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can delete tasks.");

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");

        if (task.Status == TaskStatus.Approved)
            throw new ValidationException("Approved tasks cannot be deleted.");

        _db.GetRepo<AppTask>().Delete(task);
        await _db.SaveAsync();
    }

    public async Task<TaskResponse> UpdateTaskAsync(Guid projectId, Guid taskId, UpdateTaskRequest request, Guid userId)
    {
        var membership = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == projectId && m.UserId == userId))
            .FirstOrDefault() ?? throw new ForbiddenException("You are not a member of this project.");

        if (membership.Role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can edit tasks.");

        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId) ?? throw new NotFoundException("Task not found.");
        if (task.ProjectId != projectId) throw new NotFoundException("Task not found in this project.");

        if (task.Status == TaskStatus.Approved)
            throw new ValidationException("Approved tasks cannot be edited.");

        if (request.Title != null) task.Title = request.Title;
        if (request.Description != null) task.Description = request.Description;
        if (request.Difficulty.HasValue) task.Difficulty = request.Difficulty.Value;
        if (request.DueDate.HasValue) task.DueDate = request.DueDate.Value;

        _db.GetRepo<AppTask>().Update(task);
        await _db.SaveAsync();
        return await BuildTaskResponseAsync(task);
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private async Task<ProjectRole?> GetMemberRoleAsync(Guid projectId, Guid userId)
    {
        var members = await _db.GetRepo<ProjectMember>().FindAsync(m => m.ProjectId == projectId && m.UserId == userId);
        return members.FirstOrDefault()?.Role;
    }

    private async Task EnsureMemberAsync(Guid projectId, Guid userId)
    {
        if (await GetMemberRoleAsync(projectId, userId) == null)
            throw new ForbiddenException("You are not a member of this project.");
    }

    private async Task<TaskResponse> BuildTaskResponseAsync(AppTask task)
    {
        var userRepo = _db.GetRepo<User>();
        var creator = await userRepo.GetByIdAsync(task.CreatedById);
        User? assignee = task.AssignedToId.HasValue ? await userRepo.GetByIdAsync(task.AssignedToId.Value) : null;

        var eligibleUserIds = await _db.GetRepo<TaskEligibleUser>().Query()
            .Where(eu => eu.TaskId == task.Id).Select(eu => eu.UserId).ToListAsync();

        return new TaskResponse
        {
            Id = task.Id,
            Title = task.Title,
            Description = task.Description,
            ProjectId = task.ProjectId,
            CreatedById = task.CreatedById,
            CreatedByUsername = creator?.Username ?? "",
            AssignedToId = task.AssignedToId,
            AssignedToUsername = assignee?.Username,
            Difficulty = task.Difficulty,
            Status = task.Status,
            AssignmentMode = task.AssignmentMode,
            RequiresAttachment = task.RequiresAttachment,
            RejectionFeedback = task.RejectionFeedback,
            CreatedAt = task.CreatedAt,
            DueDate = task.DueDate,
            ImageUrl = task.ImageUrl,
            EligibleUserIds = eligibleUserIds
        };
    }
}
