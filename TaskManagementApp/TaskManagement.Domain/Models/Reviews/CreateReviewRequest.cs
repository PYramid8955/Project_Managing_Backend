using System.ComponentModel.DataAnnotations;
using TaskManagement.Domain.Enums;

namespace TaskManagement.Domain.Models.Reviews;

public class CreateReviewRequest
{
    [Required]
    public ReviewStatus Status { get; set; }

    public string? Feedback { get; set; }
}
