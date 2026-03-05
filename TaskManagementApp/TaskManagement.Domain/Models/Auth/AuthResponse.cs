namespace TaskManagement.Domain.Models.Auth;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
