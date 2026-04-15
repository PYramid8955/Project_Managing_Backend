using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.Domain.Models.Auth;

namespace TaskManagement.Api.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController : BaseController
{
    private readonly IUserStatsService _userStatsService;
    private readonly IUserService _userService;

    public UsersController(IUserStatsService userStatsService, IUserService userService)
    {
        _userStatsService = userStatsService;
        _userService = userService;
    }

    /// <summary>Get stats for the currently authenticated user.</summary>
    [HttpGet("me/stats")]
    public async Task<IActionResult> GetMyStats()
    {
        var result = await _userStatsService.GetMyStatsAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>Get stats for a specific member in a project (Admin/Manager only).</summary>
    [HttpGet("~/api/projects/{projectId:guid}/members/{userId:guid}/stats")]
    public async Task<IActionResult> GetMemberStats(Guid projectId, Guid userId)
    {
        var result = await _userStatsService.GetMemberStatsAsync(projectId, userId, GetUserId());
        return Ok(result);
    }

    /// <summary>Get all active tasks assigned to current user across all projects.</summary>
    [HttpGet("me/tasks")]
    public async Task<IActionResult> GetMyTasks([FromServices] ITaskService taskService)
    {
        var result = await taskService.GetMyAssignedTasksAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>Search users by username or email (for invite flow).</summary>
    [HttpGet("search")]
    public async Task<IActionResult> SearchUsers([FromQuery] string q)
    {
        var result = await _userStatsService.SearchUsersAsync(q, GetUserId());
        return Ok(result);
    }

    /// <summary>Get the public profile of any user (shows only shared project contributions).</summary>
    [HttpGet("{userId:guid}/profile")]
    public async Task<IActionResult> GetPublicProfile(Guid userId)
    {
        var result = await _userService.GetPublicProfileAsync(userId, GetUserId());
        return Ok(result);
    }

    /// <summary>Upload or replace the current user's avatar image.</summary>
    [HttpPost("me/avatar")]
    public async Task<IActionResult> UploadAvatar(IFormFile file)
    {
        var wwwRoot = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var url = await _userService.UploadAvatarAsync(GetUserId(), file, wwwRoot);
        return Ok(new { avatarUrl = url });
    }

    /// <summary>Update username and/or email for the current user.</summary>
    [HttpPatch("me/profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
    {
        var result = await _userService.UpdateProfileAsync(GetUserId(), request);
        return Ok(result);
    }

    /// <summary>Soft-delete the current user's account and notify project admins.</summary>
    [HttpDelete("me")]
    public async Task<IActionResult> DeleteAccount()
    {
        await _userService.DeleteAccountAsync(GetUserId());
        return NoContent();
    }
}
