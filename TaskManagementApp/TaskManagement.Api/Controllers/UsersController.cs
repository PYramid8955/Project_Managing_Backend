using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;

namespace TaskManagement.Api.Controllers;

[Route("api/users")]
[Authorize]
public class UsersController : BaseController
{
    private readonly IUserStatsService _userStatsService;

    public UsersController(IUserStatsService userStatsService)
    {
        _userStatsService = userStatsService;
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
}
