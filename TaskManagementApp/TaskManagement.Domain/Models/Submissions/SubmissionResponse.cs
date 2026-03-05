using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Submissions;

public class SubmissionResponse
{
    public Guid Id { get; set; }
    public Guid TaskId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public Guid SubmittedById { get; set; }
    public string SubmitterUsername { get; set; } = string.Empty;
    public string? FileUrl { get; set; }
    public string? Comment { get; set; }
    public DateTime SubmittedAt { get; set; }
    public ReviewStatus? ReviewStatus { get; set; }
    public string? ReviewFeedback { get; set; }
}
