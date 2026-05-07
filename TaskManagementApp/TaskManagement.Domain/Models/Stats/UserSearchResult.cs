namespace TaskManagement.Domain.Models.Stats;

public class UserSearchResult
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}
