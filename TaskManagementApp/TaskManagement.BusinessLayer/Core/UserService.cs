using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure;
using TaskManagement.BusinessLayer.Structure.Exceptions;
using TaskManagement.DataAccess;
using TaskManagement.Domain;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;
using TaskManagement.Domain.Models.Auth;
using TaskManagement.Domain.Models.Users;

namespace TaskManagement.BusinessLayer.Core;

public class UserService : IUserService
{
    private readonly DbSession _db;
    private readonly JwtHelper _jwt;
    private readonly INotificationService _notifications;

    public UserService(DbSession db, JwtHelper jwt, INotificationService notifications)
    {
        _db = db;
        _jwt = jwt;
        _notifications = notifications;
    }

    public async Task<PublicUserProfileResponse> GetPublicProfileAsync(Guid targetUserId, Guid requestingUserId)
    {
        var target = await _db.GetRepo<User>().GetByIdAsync(targetUserId)
            ?? throw new NotFoundException("User not found.");

        // Shared projects: both users are members
        var targetMemberships = await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.UserId == targetUserId);

        var requesterProjectIds = (await _db.GetRepo<ProjectMember>()
            .FindAsync(m => m.UserId == requestingUserId))
            .Select(m => m.ProjectId)
            .ToHashSet();

        var sharedMemberships = targetMemberships
            .Where(m => requesterProjectIds.Contains(m.ProjectId))
            .ToList();

        var sharedProjectIds = sharedMemberships.Select(m => m.ProjectId).ToList();

        // Approved tasks per shared project
        var approvedTasks = await _db.GetRepo<AppTask>()
            .FindAsync(t => t.AssignedToId == targetUserId
                         && sharedProjectIds.Contains(t.ProjectId)
                         && t.Status == TaskStatus.Approved);

        var tasksByProject = approvedTasks
            .GroupBy(t => t.ProjectId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var projects = (await _db.GetRepo<Project>()
            .FindAsync(p => sharedProjectIds.Contains(p.Id)))
            .ToDictionary(p => p.Id);

        var contributions = sharedMemberships.Select(m =>
        {
            var tasks = tasksByProject.TryGetValue(m.ProjectId, out var list) ? list : new();
            var points = tasks.Sum(t => t.Difficulty switch
            {
                TaskDifficulty.Easy => 1,
                TaskDifficulty.Medium => 3,
                TaskDifficulty.Hard => 5,
                _ => 0
            });
            return new PublicProjectContribution
            {
                ProjectId = m.ProjectId,
                ProjectName = projects.TryGetValue(m.ProjectId, out var p) ? p.Name : string.Empty,
                Role = m.Role.ToString(),
                ApprovedTasks = tasks.Count,
                Points = points
            };
        }).ToList();

        return new PublicUserProfileResponse
        {
            UserId = target.Id,
            Username = target.Username,
            AvatarUrl = target.AvatarUrl,
            MemberSince = target.CreatedAt,
            IsDeleted = target.IsDeleted,
            TotalApprovedTasks = contributions.Sum(c => c.ApprovedTasks),
            TotalPoints = contributions.Sum(c => c.Points),
            SharedProjects = contributions
        };
    }

    public async Task<string> UploadAvatarAsync(Guid userId, IFormFile file, string webRootPath)
    {
        var user = await _db.GetRepo<User>().GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        var allowedExts = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExts.Contains(ext))
            throw new ValidationException("Only image files are allowed (png, jpg, jpeg, gif, webp).");

        if (file.Length > 5 * 1024 * 1024)
            throw new ValidationException("Avatar image must be under 5MB.");

        var avatarDir = Path.Combine(webRootPath, "uploads", "avatars");
        Directory.CreateDirectory(avatarDir);

        // Delete old avatar file if it exists
        if (!string.IsNullOrEmpty(user.AvatarUrl))
        {
            var oldPath = Path.Combine(webRootPath, user.AvatarUrl.TrimStart('/'));
            if (File.Exists(oldPath)) File.Delete(oldPath);
        }

        var fileName = $"{userId}{ext}";
        var fullPath = Path.Combine(avatarDir, fileName);

        await using var stream = new FileStream(fullPath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/uploads/avatars/{fileName}";
        user.AvatarUrl = url;
        _db.GetRepo<User>().Update(user);
        await _db.SaveAsync();

        return url;
    }

    public async Task DeleteAccountAsync(Guid userId)
    {
        var user = await _db.GetRepo<User>().GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        // Collect admin user IDs from all projects before removing memberships
        var memberships = await _db.GetRepo<ProjectMember>().FindAsync(m => m.UserId == userId);
        var projectIds = memberships.Select(m => m.ProjectId).ToList();

        var adminMemberships = await _db.GetRepo<ProjectMember>()
            .FindAsync(m => projectIds.Contains(m.ProjectId) && m.Role == ProjectRole.Admin && m.UserId != userId);

        // Soft-delete user
        user.IsDeleted = true;
        user.DeletedAt = DateTime.UtcNow;
        _db.GetRepo<User>().Update(user);

        // Remove from all project memberships
        foreach (var m in memberships)
            _db.GetRepo<ProjectMember>().Delete(m);

        await _db.SaveAsync();

        // Notify every admin of each shared project
        var notifiedAdmins = new HashSet<(Guid, Guid)>(); // (adminId, projectId)
        foreach (var admin in adminMemberships)
        {
            if (!notifiedAdmins.Add((admin.UserId, admin.ProjectId))) continue;
            await _notifications.CreateAsync(
                admin.UserId,
                NotificationType.MemberLeft,
                "Member left",
                $"{user.Username} deleted their account and has left the project.",
                userId,
                "User"
            );
        }
    }

    public async Task<AuthResponse> UpdateProfileAsync(Guid userId, UpdateProfileRequest request)
    {
        var user = await _db.GetRepo<User>().GetByIdAsync(userId)
            ?? throw new NotFoundException("User not found.");

        if (request.Username != null)
        {
            var taken = (await _db.GetRepo<User>().FindAsync(u => u.Username == request.Username && u.Id != userId)).Any();
            if (taken) throw new ConflictException("Username is already taken.");
            user.Username = request.Username;
        }

        if (request.Email != null)
        {
            var emailTaken = (await _db.GetRepo<User>().FindAsync(u => u.Email == request.Email.ToLower() && u.Id != userId)).Any();
            if (emailTaken) throw new ConflictException("Email is already in use.");
            user.Email = request.Email.ToLower();
        }

        _db.GetRepo<User>().Update(user);
        await _db.SaveAsync();

        return new AuthResponse
        {
            Token = _jwt.GenerateToken(user),
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            AvatarUrl = user.AvatarUrl
        };
    }
}
