using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Tasks;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.BusinessLayer.Core;

public class TaskService : ITaskService
{
    private readonly DbSession _db;
    private readonly INotificationService _notifications;

    public TaskService(DbSession db, INotificationService notifications)
    {
        _db = db;
        _notifications = notifications;
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
            Description = request.Description ?? string.Empty,
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

        // Notify direct assignee if someone else created the task for them
        if (task.AssignmentMode == AssignmentMode.Direct
            && task.AssignedToId.HasValue
            && task.AssignedToId.Value != userId)
        {
            await _notifications.CreateAsync(
                task.AssignedToId.Value,
                NotificationType.TaskAssigned,
                "New task assigned",
                $"You have been assigned the task \"{task.Title}\".",
                task.Id,
                "Task"
            );
        }

        return await BuildTaskResponseAsync(task);
    }

    public async Task<IEnumerable<TaskResponse>> GetProjectTasksAsync(Guid projectId, Guid userId, string? status = null, string? difficulty = null, string? sortBy = null, Guid? assignedToFilter = null)
    {
        await EnsureMemberAsync(projectId, userId);

        var query = _db.GetRepo<AppTask>().Query().Where(t => t.ProjectId == projectId);

        if (!string.IsNullOrEmpty(status) && Enum.TryParse<TaskStatus>(status, out var parsedStatus))
            query = query.Where(t => t.Status == parsedStatus);

        if (!string.IsNullOrEmpty(difficulty) && Enum.TryParse<TaskDifficulty>(difficulty, out var parsedDiff))
            query = query.Where(t => t.Difficulty == parsedDiff);

        if (assignedToFilter.HasValue)
            query = query.Where(t => t.AssignedToId == assignedToFilter.Value);

        query = sortBy switch
        {
            "dueDate" => query.OrderBy(t => t.DueDate == null).ThenBy(t => t.DueDate),
            "difficulty" => query.OrderByDescending(t => t.Difficulty),
            "status" => query.OrderBy(t => t.Status),
            _ => query.OrderByDescending(t => t.CreatedAt)
        };

        var tasks = await query.ToListAsync();
        return await BuildTaskResponsesAsync(tasks);
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

        if (request.NewStatus == TaskStatus.Approved || request.NewStatus == TaskStatus.Rejected)
            throw new ValidationException("Use the review endpoint to approve or reject submitted tasks.");

        if (role == ProjectRole.Developer)
        {
            // Developer can only accept (ToDo → InProgress)
            if (request.NewStatus != TaskStatus.InProgress || task.Status != TaskStatus.ToDo)
                throw new ForbiddenException("Developers can only accept tasks (ToDo → InProgress).");

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
        else if (role == ProjectRole.Manager && task.Status == TaskStatus.ToDo && request.NewStatus == TaskStatus.InProgress)
        {
            // Manager accepting an open task — same eligibility checks + auto-assign
            if (task.AssignmentMode == AssignmentMode.Direct)
                throw new ForbiddenException("This task is directly assigned and does not require acceptance.");

            if (task.AssignmentMode == AssignmentMode.Invited)
            {
                var isEligible = await _db.GetRepo<TaskEligibleUser>()
                    .AnyAsync(eu => eu.TaskId == taskId && eu.UserId == userId);
                if (!isEligible)
                    throw new ForbiddenException("You are not in the list of eligible members for this task.");
            }

            if (task.AssignedToId != null && task.AssignedToId != userId)
                throw new ForbiddenException("This task is assigned to another member.");

            task.Status = TaskStatus.InProgress;
            task.RejectionFeedback = null;
            if (task.AssignedToId == null)
                task.AssignedToId = userId;
        }
        else
        {
            // Admin, or Manager doing other status overrides
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

        await _notifications.CreateAsync(
            request.DeveloperUserId,
            NotificationType.TaskDelegated,
            "Task delegated to you",
            $"A task has been delegated to you: \"{task.Title}\".",
            task.Id,
            "Task"
        );

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

        if (request.AssignedToId.HasValue && request.AssignedToId.Value != userId)
        {
            await _notifications.CreateAsync(
                request.AssignedToId.Value,
                NotificationType.TaskAssigned,
                "Task assigned to you",
                $"You have been assigned the task \"{task.Title}\".",
                task.Id,
                "Task"
            );
        }

        return await BuildTaskResponseAsync(task);
    }

    public async Task<IEnumerable<TaskResponse>> GetMyAssignedTasksAsync(Guid userId)
    {
        var tasks = await _db.GetRepo<AppTask>().Query()
            .Where(t => t.AssignedToId == userId && t.Status != TaskStatus.Approved)
            .OrderBy(t => t.DueDate == null)
            .ThenBy(t => t.DueDate)
            .ToListAsync();

        return await BuildTaskResponsesAsync(tasks);
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
        var responses = await BuildTaskResponsesAsync([task]);
        return responses[0];
    }

    private async Task<List<TaskResponse>> BuildTaskResponsesAsync(IList<AppTask> tasks)
    {
        if (tasks.Count == 0) return [];

        var userIds = tasks.Select(t => t.CreatedById)
            .Concat(tasks.Where(t => t.AssignedToId.HasValue).Select(t => t.AssignedToId!.Value))
            .Distinct().ToList();

        var users = await _db.GetRepo<User>().Query()
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        var taskIds = tasks.Select(t => t.Id).ToList();
        var eligibleMap = await _db.GetRepo<TaskEligibleUser>().Query()
            .Where(eu => taskIds.Contains(eu.TaskId))
            .GroupBy(eu => eu.TaskId)
            .ToDictionaryAsync(g => g.Key, g => g.Select(eu => eu.UserId).ToList());

        return tasks.Select(task =>
        {
            users.TryGetValue(task.CreatedById, out var creator);
            users.TryGetValue(task.AssignedToId ?? Guid.Empty, out var assignee);
            return new TaskResponse
            {
                Id = task.Id,
                Title = task.Title,
                Description = task.Description,
                ProjectId = task.ProjectId,
                CreatedById = task.CreatedById,
                CreatedByUsername = creator?.Username ?? "",
                AssignedToId = task.AssignedToId,
                AssignedToUsername = task.AssignedToId.HasValue ? assignee?.Username : null,
                Difficulty = task.Difficulty,
                Status = task.Status,
                AssignmentMode = task.AssignmentMode,
                RequiresAttachment = task.RequiresAttachment,
                RejectionFeedback = task.RejectionFeedback,
                CreatedAt = task.CreatedAt,
                DueDate = task.DueDate,
                ImageUrl = task.ImageUrl,
                EligibleUserIds = eligibleMap.GetValueOrDefault(task.Id, [])
            };
        }).ToList();
    }
}
