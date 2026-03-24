using System.ComponentModel.DataAnnotations;

namespace TaskManagement.Domain.Models.Auth;

public class UpdateProfileRequest
{
    [MinLength(3)]
    [MaxLength(50)]
    public string? Username { get; set; }
}
