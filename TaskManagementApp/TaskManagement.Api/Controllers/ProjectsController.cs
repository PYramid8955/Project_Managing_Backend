using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure;
using TaskManagement.Domain.Models.Invitations;
using TaskManagement.Domain.Models.Projects;
using TaskManagement.Domain.Models.Tasks;

namespace TaskManagement.Api.Controllers;

[Route("api/projects")]
[Authorize]
public class ProjectsController : BaseController
{
    private readonly IProjectService _projectService;
    private readonly IProjectInvitationService _invitationService;
    private readonly ITaskService _taskService;
    private readonly IStatsService _statsService;
    private readonly FileStorageHelper _fileStorage;
    private readonly IWebHostEnvironment _env;
    private readonly IConfiguration _config;

    public ProjectsController(
        IProjectService projectService,
        IProjectInvitationService invitationService,
        ITaskService taskService,
        IStatsService statsService,
        FileStorageHelper fileStorage,
        IWebHostEnvironment env,
        IConfiguration config)
    {
        _projectService = projectService;
        _invitationService = invitationService;
        _taskService = taskService;
        _statsService = statsService;
        _fileStorage = fileStorage;
        _env = env;
        _config = config;
    }

    // ─── Project CRUD ────────────────────────────────────────────────────────

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectRequest request)
    {
        var result = await _projectService.CreateProjectAsync(request, GetUserId());
        return CreatedAtAction(nameof(GetProject), new { id = result.Id }, result);
    }

    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        var result = await _projectService.GetUserProjectsAsync(GetUserId());
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var result = await _projectService.GetProjectByIdAsync(id, GetUserId());
        return Ok(result);
    }

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectRequest request)
    {
        var result = await _projectService.UpdateProjectAsync(id, request, GetUserId());
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        await _projectService.DeleteProjectAsync(id, GetUserId());
        return NoContent();
    }

    // ─── Member Management ───────────────────────────────────────────────────

    [HttpGet("{id:guid}/members")]
    public async Task<IActionResult> GetMembers(Guid id)
    {
        var result = await _projectService.GetMembersAsync(id, GetUserId());
        return Ok(result);
    }

    [HttpPost("{id:guid}/invitations")]
    public async Task<IActionResult> SendInvitation(Guid id, [FromBody] SendInvitationRequest request)
    {
        var result = await _invitationService.SendInvitationAsync(id, request, GetUserId());
        return Ok(result);
    }

    [HttpGet("{id:guid}/invitations")]
    public async Task<IActionResult> GetInvitations(Guid id)
    {
        var result = await _invitationService.GetProjectInvitationsAsync(id, GetUserId());
        return Ok(result);
    }

    [HttpDelete("{id:guid}/invitations/{invitationId:guid}")]
    public async Task<IActionResult> CancelInvitation(Guid id, Guid invitationId)
    {
        await _invitationService.CancelInvitationAsync(id, invitationId, GetUserId());
        return NoContent();
    }

    [HttpPost("{id:guid}/invite-link")]
    public async Task<IActionResult> GenerateInviteLink(Guid id)
    {
        var frontendBase = _config["Frontend:BaseUrl"] ?? "http://localhost:5173";
        var result = await _invitationService.GenerateInviteLinkAsync(id, GetUserId(), frontendBase);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId)
    {
        await _projectService.RemoveMemberAsync(id, userId, GetUserId());
        return Ok(new { message = "Member removed successfully." });
    }

    [HttpPatch("{id:guid}/members/{userId:guid}/role")]
    public async Task<IActionResult> UpdateMemberRole(Guid id, Guid userId, [FromBody] UpdateMemberRoleRequest request)
    {
        await _projectService.UpdateMemberRoleAsync(id, userId, request, GetUserId());
        return Ok(new { message = "Role updated successfully." });
    }

    /// <summary>Assign a developer to a manager's group (Admin only).</summary>
    [HttpPatch("{id:guid}/members/{userId:guid}/manager")]
    public async Task<IActionResult> AssignManager(Guid id, Guid userId, [FromBody] AssignManagerRequest request)
    {
        await _projectService.AssignDeveloperToManagerAsync(id, userId, request, GetUserId());
        return Ok(new { message = "Group assignment updated." });
    }

    // ─── Task Management ─────────────────────────────────────────────────────

    [HttpPost("{projectId:guid}/tasks")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<IActionResult> CreateTask(Guid projectId, [FromForm] CreateTaskRequest request, IFormFile? image)
    {
        string? imageUrl = null;
        if (image != null)
        {
            var imageExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var ext = Path.GetExtension(image.FileName);
            if (!imageExtensions.Contains(ext))
                return BadRequest(new { message = "Only image files are allowed (jpg, png, gif, webp)." });
            imageUrl = await _fileStorage.SaveFileAsync(image, Guid.NewGuid(), _env.WebRootPath);
        }

        var result = await _taskService.CreateTaskAsync(projectId, request, GetUserId(), imageUrl);
        return CreatedAtAction(nameof(GetTask), new { projectId, id = result.Id }, result);
    }

    [HttpGet("{projectId:guid}/tasks")]
    public async Task<IActionResult> GetTasks(Guid projectId, [FromQuery] string? status, [FromQuery] string? difficulty, [FromQuery] string? sortBy)
    {
        var result = await _taskService.GetProjectTasksAsync(projectId, GetUserId(), status, difficulty, sortBy);
        return Ok(result);
    }

    [HttpGet("{projectId:guid}/tasks/{id:guid}")]
    public async Task<IActionResult> GetTask(Guid projectId, Guid id)
    {
        var result = await _taskService.GetTaskByIdAsync(projectId, id, GetUserId());
        return Ok(result);
    }

    [HttpPatch("{projectId:guid}/tasks/{id:guid}/status")]
    public async Task<IActionResult> UpdateTaskStatus(Guid projectId, Guid id, [FromBody] UpdateTaskStatusRequest request)
    {
        var result = await _taskService.UpdateTaskStatusAsync(projectId, id, request, GetUserId());
        return Ok(result);
    }

    /// <summary>Manager delegates their InProgress task to a developer in their group.</summary>
    [HttpPost("{projectId:guid}/tasks/{id:guid}/delegate")]
    public async Task<IActionResult> DelegateTask(Guid projectId, Guid id, [FromBody] DelegateTaskRequest request)
    {
        var result = await _taskService.DelegateTaskAsync(projectId, id, request, GetUserId());
        return Ok(result);
    }

    [HttpPatch("{projectId:guid}/tasks/{id:guid}/assign")]
    public async Task<IActionResult> ReassignTask(Guid projectId, Guid id, [FromBody] ReassignTaskRequest request)
    {
        var result = await _taskService.ReassignTaskAsync(projectId, id, request, GetUserId());
        return Ok(result);
    }

    [HttpDelete("{projectId:guid}/tasks/{id:guid}")]
    public async Task<IActionResult> DeleteTask(Guid projectId, Guid id)
    {
        await _taskService.DeleteTaskAsync(projectId, id, GetUserId());
        return NoContent();
    }

    [HttpPatch("{projectId:guid}/tasks/{id:guid}")]
    public async Task<IActionResult> UpdateTask(Guid projectId, Guid id, [FromBody] UpdateTaskRequest request)
    {
        var result = await _taskService.UpdateTaskAsync(projectId, id, request, GetUserId());
        return Ok(result);
    }

    // ─── Stats ───────────────────────────────────────────────────────────────

    [HttpGet("{projectId:guid}/stats")]
    public async Task<IActionResult> GetStats(Guid projectId)
    {
        var result = await _statsService.GetProjectStatsAsync(projectId, GetUserId());
        return Ok(result);
    }

    [HttpGet("{projectId:guid}/stats/ranking")]
    public async Task<IActionResult> GetRanking(Guid projectId)
    {
        var result = await _statsService.GetProjectRankingAsync(projectId, GetUserId());
        return Ok(result);
    }
}
