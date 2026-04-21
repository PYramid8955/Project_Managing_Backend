using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Submissions;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.BusinessLayer.Core;

public class SubmissionService : ISubmissionService
{
    private readonly DbSession _db;
    private readonly FileStorageHelper _fileStorage;
    private readonly INotificationService _notifications;

    public SubmissionService(DbSession db, FileStorageHelper fileStorage, INotificationService notifications)
    {
        _db = db;
        _fileStorage = fileStorage;
        _notifications = notifications;
    }

    public async Task<SubmissionResponse> CreateSubmissionAsync(
        Guid taskId, IFormFile? file, string? comment, Guid userId, string wwwrootPath)
    {
        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId)
            ?? throw new NotFoundException("Task not found.");

        if (task.AssignedToId != userId)
            throw new ForbiddenException("You are not assigned to this task.");

        if (task.Status != TaskStatus.InProgress)
            throw new ValidationException("Task must be In Progress before submitting.");

        // Enforce attachment requirement
        if (task.RequiresAttachment && (file == null || file.Length == 0))
            throw new ValidationException("This task requires a file attachment.");

        var existingSubmissions = await _db.GetRepo<TaskSubmission>()
            .Query()
            .Where(s => s.TaskId == taskId)
            .Include(s => s.Review)
            .ToListAsync();

        if (existingSubmissions.Any(s => s.Review == null))
            throw new ConflictException("An active submission already exists for this task. Wait for the review.");

        string? fileUrl = null;
        if (file != null && file.Length > 0)
            fileUrl = await _fileStorage.SaveFileAsync(file, taskId, wwwrootPath);

        var submission = new TaskSubmission
        {
            TaskId = taskId,
            SubmittedById = userId,
            FileUrl = fileUrl,
            Comment = comment
        };

        await _db.GetRepo<TaskSubmission>().AddAsync(submission);

        task.Status = TaskStatus.Submitted;
        _db.GetRepo<AppTask>().Update(task);

        await _db.SaveAsync();

        // Notify all admins and managers in the project about the new submission
        var reviewers = await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == task.ProjectId
                         && (m.Role == ProjectRole.Admin || m.Role == ProjectRole.Manager));

        var submitter = await _db.GetRepo<User>().GetByIdAsync(userId);

        foreach (var reviewer in reviewers)
        {
            await _notifications.CreateAsync(
                reviewer.UserId,
                NotificationType.TaskSubmitted,
                "Task submitted for review",
                $"{submitter?.Username ?? "A developer"} submitted \"{task.Title}\" for review.",
                task.Id,
                "Task"
            );
        }

        return new SubmissionResponse
        {
            Id = submission.Id,
            TaskId = task.Id,
            TaskTitle = task.Title,
            SubmittedById = submission.SubmittedById,
            SubmitterUsername = submitter?.Username ?? "",
            FileUrl = submission.FileUrl,
            Comment = submission.Comment,
            SubmittedAt = submission.SubmittedAt,
            ReviewStatus = null
        };
    }

    public async Task<IEnumerable<SubmissionResponse>> GetTaskSubmissionsAsync(Guid taskId, Guid userId)
    {
        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId)
            ?? throw new NotFoundException("Task not found.");

        var isMember = await _db.GetRepo<ProjectMember>()
            .AnyAsync(m => m.ProjectId == task.ProjectId && m.UserId == userId);
        if (!isMember)
            throw new ForbiddenException("You are not a member of this project.");

        var submissions = await _db.GetRepo<TaskSubmission>()
            .Query()
            .Where(s => s.TaskId == taskId)
            .Include(s => s.Review)
            .OrderByDescending(s => s.SubmittedAt)
            .ToListAsync();

        var userRepo = _db.GetRepo<User>();
        var result = new List<SubmissionResponse>();

        foreach (var s in submissions)
        {
            var submitter = await userRepo.GetByIdAsync(s.SubmittedById);
            result.Add(new SubmissionResponse
            {
                Id = s.Id,
                TaskId = s.TaskId,
                TaskTitle = task.Title,
                SubmittedById = s.SubmittedById,
                SubmitterUsername = submitter?.Username ?? "",
                FileUrl = s.FileUrl,
                Comment = s.Comment,
                SubmittedAt = s.SubmittedAt,
                ReviewStatus = s.Review?.Status,
                ReviewFeedback = s.Review?.Feedback
            });
        }

        return result;
    }

    public async Task CancelSubmissionAsync(Guid taskId, Guid submissionId, Guid userId)
    {
        var task = await _db.GetRepo<AppTask>().GetByIdAsync(taskId)
            ?? throw new NotFoundException("Task not found.");

        if (task.AssignedToId != userId)
            throw new ForbiddenException("You are not assigned to this task.");

        if (task.Status != TaskStatus.Submitted)
            throw new ValidationException("Only submitted tasks can have their submission cancelled.");

        var submission = await _db.GetRepo<TaskSubmission>().GetByIdAsync(submissionId)
            ?? throw new NotFoundException("Submission not found.");

        if (submission.TaskId != taskId)
            throw new ValidationException("Submission does not belong to this task.");

        if (submission.Review != null)
            throw new ValidationException("Cannot cancel a submission that has already been reviewed.");

        // Revert task to InProgress
        task.Status = TaskStatus.InProgress;
        _db.GetRepo<AppTask>().Update(task);
        _db.GetRepo<TaskSubmission>().Delete(submission);
        await _db.SaveAsync();
    }
}
