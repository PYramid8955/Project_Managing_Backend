using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Models.Auth;

namespace TaskManagement.BusinessLayer.Core;

public class AuthService : IAuthService
{
    private readonly DbSession _db;
    private readonly JwtHelper _jwt;

    public AuthService(DbSession db, JwtHelper jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var userRepo = _db.GetRepo<User>();

        var existingUsers = await userRepo.FindAsync(u => u.Email == request.Email.ToLower());
        if (existingUsers.Any())
            throw new ConflictException("A user with this email already exists.");

        var usernameCheck = await userRepo.FindAsync(u => u.Username == request.Username);
        if (usernameCheck.Any())
            throw new ConflictException("This username is already taken.");

        var user = new User
        {
            Username = request.Username,
            Email = request.Email.ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
        };

        await userRepo.AddAsync(user);
        await _db.SaveAsync();

        return new AuthResponse
        {
            Token = _jwt.GenerateToken(user),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        };
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var userRepo = _db.GetRepo<User>();
        var users = await userRepo.FindAsync(u => u.Email == request.Email.ToLower());
        var user = users.FirstOrDefault();

        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("Invalid email or password.");

        return new AuthResponse
        {
            Token = _jwt.GenerateToken(user),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email
        };
    }

    public async Task ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _db.GetRepo<User>().GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
            throw new ValidationException("Current password is incorrect.");

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        _db.GetRepo<User>().Update(user);
        await _db.SaveAsync();
    }
}
