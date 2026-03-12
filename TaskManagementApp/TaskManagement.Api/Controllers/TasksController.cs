using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.Domain.Models.Tasks;

namespace TaskManagement.Api.Controllers;

[Route("api/tasks")]
[Authorize]
public class TasksController : BaseController
{
    private readonly ISubmissionService _submissionService;
    private readonly ICommentService _commentService;
    private readonly IWebHostEnvironment _env;

    public TasksController(
        ISubmissionService submissionService,
        ICommentService commentService,
        IWebHostEnvironment env)
    {
        _submissionService = submissionService;
        _commentService = commentService;
        _env = env;
    }

    /// <summary>Submit a completed task. File is optional unless RequiresAttachment is true.</summary>
    [HttpPost("{taskId:guid}/submissions")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateSubmission(
        Guid taskId,
        IFormFile? file,
        [FromForm] string? comment)
    {
        var result = await _submissionService.CreateSubmissionAsync(
            taskId, file, comment, GetUserId(), _env.WebRootPath);
        return CreatedAtAction(nameof(GetSubmissions), new { taskId }, result);
    }

    /// <summary>Get all submissions for a task.</summary>
    [HttpGet("{taskId:guid}/submissions")]
    public async Task<IActionResult> GetSubmissions(Guid taskId)
    {
        var result = await _submissionService.GetTaskSubmissionsAsync(taskId, GetUserId());
        return Ok(result);
    }

    /// <summary>Add a comment to a task.</summary>
    [HttpPost("{projectId:guid}/{taskId:guid}/comments")]
    public async Task<IActionResult> AddComment(Guid projectId, Guid taskId, [FromBody] TaskCommentRequest request)
    {
        var result = await _commentService.AddCommentAsync(projectId, taskId, request, GetUserId());
        return CreatedAtAction(nameof(GetComments), new { projectId, taskId }, result);
    }

    /// <summary>Get all comments for a task.</summary>
    [HttpGet("{projectId:guid}/{taskId:guid}/comments")]
    public async Task<IActionResult> GetComments(Guid projectId, Guid taskId)
    {
        var result = await _commentService.GetTaskCommentsAsync(projectId, taskId, GetUserId());
        return Ok(result);
    }
}
