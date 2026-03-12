using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.Domain.Models.Reviews;

namespace TaskManagement.Api.Controllers;

[Route("api/submissions")]
[Authorize]
public class SubmissionsController : BaseController
{
    private readonly IReviewService _reviewService;

    public SubmissionsController(IReviewService reviewService)
    {
        _reviewService = reviewService;
    }

    /// <summary>Review a submission (Admin/Manager only).</summary>
    [HttpPost("{submissionId:guid}/reviews")]
    public async Task<IActionResult> CreateReview(Guid submissionId, [FromBody] CreateReviewRequest request)
    {
        var result = await _reviewService.CreateReviewAsync(submissionId, request, GetUserId());
        return CreatedAtAction(nameof(CreateReview), new { submissionId }, result);
    }
}
