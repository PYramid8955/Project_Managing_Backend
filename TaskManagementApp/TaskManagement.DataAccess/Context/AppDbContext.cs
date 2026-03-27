using Microsoft.EntityFrameworkCore;
using TaskManagement.Domain.Entities;
using TaskManagement.Domain.Enums;
using TaskStatus = TaskManagement.Domain.Enums.TaskStatus;

namespace TaskManagement.DataAccess.Context;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<AppTask> Tasks => Set<AppTask>();
    public DbSet<TaskSubmission> TaskSubmissions => Set<TaskSubmission>();
    public DbSet<TaskReview> TaskReviews => Set<TaskReview>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();
    public DbSet<TaskEligibleUser> TaskEligibleUsers => Set<TaskEligibleUser>();
    public DbSet<ProjectInvitation> ProjectInvitations => Set<ProjectInvitation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Email).IsUnique();
            e.Property(u => u.Id).HasColumnType("uuid");
        });

        // Project
        modelBuilder.Entity<Project>(e =>
        {
            e.HasKey(p => p.Id);
            e.Property(p => p.Id).HasColumnType("uuid");
            e.Property(p => p.CreatedById).HasColumnType("uuid");

            e.HasOne(p => p.CreatedBy)
                .WithMany(u => u.CreatedProjects)
                .HasForeignKey(p => p.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasMany(p => p.Members)
                .WithOne(m => m.Project)
                .HasForeignKey(m => m.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(p => p.Tasks)
                .WithOne(t => t.Project)
                .HasForeignKey(t => t.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // ProjectMember
        modelBuilder.Entity<ProjectMember>(e =>
        {
            e.HasKey(pm => pm.Id);
            e.Property(pm => pm.Id).HasColumnType("uuid");
            e.Property(pm => pm.UserId).HasColumnType("uuid");
            e.Property(pm => pm.ProjectId).HasColumnType("uuid");

            e.HasIndex(pm => new { pm.UserId, pm.ProjectId }).IsUnique();
            e.Property(pm => pm.Role).HasConversion<int>();
            e.Property(pm => pm.ManagerUserId).HasColumnType("uuid");

            e.HasOne(pm => pm.User)
                .WithMany(u => u.ProjectMemberships)
                .HasForeignKey(pm => pm.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // AppTask
        modelBuilder.Entity<AppTask>(e =>
        {
            e.HasKey(t => t.Id);
            e.Property(t => t.Id).HasColumnType("uuid");
            e.Property(t => t.ProjectId).HasColumnType("uuid");
            e.Property(t => t.CreatedById).HasColumnType("uuid");
            e.Property(t => t.AssignedToId).HasColumnType("uuid");

            e.Property(t => t.Difficulty).HasConversion<int>();
            e.Property(t => t.Status).HasConversion<int>();
            e.Property(t => t.AssignmentMode).HasConversion<int>();

            e.HasOne(t => t.CreatedBy)
                .WithMany()
                .HasForeignKey(t => t.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(t => t.AssignedTo)
                .WithMany()
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);

            e.HasMany(t => t.Submissions)
                .WithOne(s => s.Task)
                .HasForeignKey(s => s.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(t => t.Comments)
                .WithOne(c => c.Task)
                .HasForeignKey(c => c.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasMany(t => t.EligibleUsers)
                .WithOne(eu => eu.Task)
                .HasForeignKey(eu => eu.TaskId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskSubmission
        modelBuilder.Entity<TaskSubmission>(e =>
        {
            e.HasKey(s => s.Id);
            e.Property(s => s.Id).HasColumnType("uuid");
            e.Property(s => s.TaskId).HasColumnType("uuid");
            e.Property(s => s.SubmittedById).HasColumnType("uuid");

            e.HasOne(s => s.SubmittedBy)
                .WithMany()
                .HasForeignKey(s => s.SubmittedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(s => s.Review)
                .WithOne(r => r.Submission)
                .HasForeignKey<TaskReview>(r => r.SubmissionId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // TaskReview
        modelBuilder.Entity<TaskReview>(e =>
        {
            e.HasKey(r => r.Id);
            e.Property(r => r.Id).HasColumnType("uuid");
            e.Property(r => r.SubmissionId).HasColumnType("uuid");
            e.Property(r => r.ReviewedById).HasColumnType("uuid");

            e.Property(r => r.Status).HasConversion<int>();

            e.HasOne(r => r.ReviewedBy)
                .WithMany()
                .HasForeignKey(r => r.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // TaskComment
        modelBuilder.Entity<TaskComment>(e =>
        {
            e.HasKey(c => c.Id);
            e.Property(c => c.Id).HasColumnType("uuid");
            e.Property(c => c.TaskId).HasColumnType("uuid");
            e.Property(c => c.UserId).HasColumnType("uuid");

            e.HasOne(c => c.User)
                .WithMany()
                .HasForeignKey(c => c.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectInvitation
        modelBuilder.Entity<ProjectInvitation>(e =>
        {
            e.HasKey(i => i.Id);
            e.Property(i => i.Id).HasColumnType("uuid");
            e.Property(i => i.ProjectId).HasColumnType("uuid");
            e.Property(i => i.InvitedById).HasColumnType("uuid");
            e.Property(i => i.InvitedUserId).HasColumnType("uuid");
            e.Property(i => i.Status).HasConversion<int>();
            e.Property(i => i.Role).HasConversion<int?>();
            e.HasIndex(i => i.Token).IsUnique();

            e.HasOne(i => i.Project)
                .WithMany()
                .HasForeignKey(i => i.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(i => i.InvitedBy)
                .WithMany()
                .HasForeignKey(i => i.InvitedById)
                .OnDelete(DeleteBehavior.Restrict);

            e.HasOne(i => i.InvitedUser)
                .WithMany()
                .HasForeignKey(i => i.InvitedUserId)
                .OnDelete(DeleteBehavior.SetNull)
                .IsRequired(false);
        });

        // TaskEligibleUser — composite PK
        modelBuilder.Entity<TaskEligibleUser>(e =>
        {
            e.HasKey(eu => new { eu.TaskId, eu.UserId });
            e.Property(eu => eu.TaskId).HasColumnType("uuid");
            e.Property(eu => eu.UserId).HasColumnType("uuid");

            e.HasOne(eu => eu.User)
                .WithMany()
                .HasForeignKey(eu => eu.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
