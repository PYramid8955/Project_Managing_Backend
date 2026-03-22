using TaskManagement.Domain.Models.Auth;

namespace TaskManagement.BusinessLayer.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}
