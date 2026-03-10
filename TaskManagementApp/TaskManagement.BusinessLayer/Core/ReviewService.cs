using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskManagement.Domain.Models.Reviews;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.BusinessLayer.Core;

public class ReviewService : IReviewService
{
    private readonly DbSession _db;

    public ReviewService(DbSession db)
    {
        _db = db;
    }

    public async Task<ReviewResponse> CreateReviewAsync(Guid submissionId, CreateReviewRequest request, Guid userId)
    {
        var submission = await _db.GetRepo<TaskSubmission>()
            .Query()
            .Include(s => s.Review)
            .Include(s => s.Task)
            .FirstOrDefaultAsync(s => s.Id == submissionId)
            ?? throw new NotFoundException("Submission not found.");

        if (submission.Review != null)
            throw new ConflictException("This submission has already been reviewed.");

        var members = await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.ProjectId == submission.Task.ProjectId && m.UserId == userId);
        var member = members.FirstOrDefault()
            ?? throw new ForbiddenException("You are not a member of this project.");

        if (member.Role == ProjectRole.Developer)
            throw new ForbiddenException("Only Admins and Managers can review submissions.");

        var review = new TaskReview
        {
            SubmissionId = submissionId,
            ReviewedById = userId,
            Status = request.Status,
            Feedback = request.Feedback
        };

        await _db.GetRepo<TaskReview>().AddAsync(review);

        var task = submission.Task;
        if (request.Status == ReviewStatus.Approved)
        {
            task.Status = TaskStatus.Approved;
            task.RejectionFeedback = null;
        }
        else
        {
            // Rejected: return to InProgress so developer can fix and re-submit
            task.Status = TaskStatus.InProgress;
            task.RejectionFeedback = request.Feedback;
        }

        _db.GetRepo<AppTask>().Update(task);
        await _db.SaveAsync();

        var reviewer = await _db.GetRepo<User>().GetByIdAsync(userId);

        return new ReviewResponse
        {
            Id = review.Id,
            SubmissionId = review.SubmissionId,
            ReviewedById = review.ReviewedById,
            ReviewedByUsername = reviewer?.Username ?? "",
            Status = review.Status,
            Feedback = review.Feedback,
            ReviewedAt = review.ReviewedAt
        };
    }
}
