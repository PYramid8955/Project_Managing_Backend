using TaskManagement.Domain.Models.Reviews;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IReviewService
{
    Task<ReviewResponse> CreateReviewAsync(Guid submissionId, CreateReviewRequest request, Guid userId);
}
