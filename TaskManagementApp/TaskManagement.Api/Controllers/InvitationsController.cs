using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskManagement.BusinessLayer.Interfaces;

namespace TaskManagement.Api.Controllers;

[Route("api/invitations")]
[Authorize]
public class InvitationsController : BaseController
{
    private readonly IProjectInvitationService _invitationService;

    public InvitationsController(IProjectInvitationService invitationService)
    {
        _invitationService = invitationService;
    }

    /// <summary>Get all pending invitations for the current user.</summary>
    [HttpGet("me")]
    public async Task<IActionResult> GetMyInvitations()
    {
        var result = await _invitationService.GetMyInvitationsAsync(GetUserId());
        return Ok(result);
    }

    /// <summary>Get invitation details by token (used on the invite link landing page).</summary>
    [HttpGet("{token}")]
    public async Task<IActionResult> GetByToken(string token)
    {
        var result = await _invitationService.GetInvitationByTokenAsync(token);
        return Ok(result);
    }

    /// <summary>Accept an invitation (email-based or link-based).</summary>
    [HttpPost("{token}/accept")]
    public async Task<IActionResult> Accept(string token)
    {
        await _invitationService.AcceptInvitationAsync(token, GetUserId());
        return Ok(new { message = "Invitation accepted. You have joined the project." });
    }

    /// <summary>Decline an invitation.</summary>
    [HttpPost("{token}/decline")]
    public async Task<IActionResult> Decline(string token)
    {
        await _invitationService.DeclineInvitationAsync(token, GetUserId());
        return Ok(new { message = "Invitation declined." });
    }
}
