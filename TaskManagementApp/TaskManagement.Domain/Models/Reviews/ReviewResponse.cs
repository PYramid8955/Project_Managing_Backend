using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Reviews;

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid SubmissionId { get; set; }
    public Guid ReviewedById { get; set; }
    public string ReviewedByUsername { get; set; } = string.Empty;
    public ReviewStatus Status { get; set; }
    public string? Feedback { get; set; }
    public DateTime ReviewedAt { get; set; }
}
