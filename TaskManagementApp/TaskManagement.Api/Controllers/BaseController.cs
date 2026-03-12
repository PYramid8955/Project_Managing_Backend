using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;

namespace TaskManagement.Api.Controllers;

[ApiController]
public abstract class BaseController : ControllerBase
{
    protected Guid GetUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? throw new UnauthorizedAccessException("User ID claim not found.");
        return Guid.Parse(value);
    }
}
