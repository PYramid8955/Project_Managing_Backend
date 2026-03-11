using Microsoft.Extensions.DependencyInjection;
using TaskManagement.BusinessLayer.Core;
using TaskManagement.BusinessLayer.Interfaces;
using TaskManagement.BusinessLayer.Structure;
using TaskManagement.DataAccess;

namespace TaskManagement.BusinessLayer;

public static class BusinessLogic
{
    /// <summary>
    /// Registers all business layer services with the DI container.
    /// Call this in Program.cs: builder.Services.AddBusinessLogic()
    /// </summary>
    public static IServiceCollection AddBusinessLogic(this IServiceCollection services)
    {
        // Data access
        services.AddScoped<DbSession>();

        // Helpers
        services.AddScoped<JwtHelper>();
        services.AddScoped<FileStorageHelper>();

        // Services
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<ITaskService, TaskService>();
        services.AddScoped<ISubmissionService, SubmissionService>();
        services.AddScoped<IReviewService, ReviewService>();
        services.AddScoped<IStatsService, StatsService>();
        services.AddScoped<ICommentService, CommentService>();
        services.AddScoped<IUserStatsService, UserStatsService>();

        return services;
    }
}
