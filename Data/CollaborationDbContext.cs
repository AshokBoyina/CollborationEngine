using CollaborationEngine.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CollaborationEngine.API.Data;

public class CollaborationDbContext : DbContext
{
    public CollaborationDbContext(DbContextOptions<CollaborationDbContext> options) : base(options)
    {
    }

    public DbSet<Application> Applications { get; set; }
    public DbSet<User> Users { get; set; }
    public DbSet<Agent> Agents { get; set; }
    public DbSet<Supervisor> Supervisors { get; set; }
    public DbSet<CollaborationSession> CollaborationSessions { get; set; }
    public DbSet<AgentSupervisorSession> AgentSupervisorSessions { get; set; }
    public DbSet<ChatMessage> ChatMessages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Application configuration
        modelBuilder.Entity<Application>()
            .HasIndex(a => a.ApiKey)
            .IsUnique();

        // User configuration
        modelBuilder.Entity<User>()
            .HasOne(u => u.Application)
            .WithMany(a => a.Users)
            .HasForeignKey(u => u.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Agent configuration
        modelBuilder.Entity<Agent>()
            .HasOne(a => a.Application)
            .WithMany(app => app.Agents)
            .HasForeignKey(a => a.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // Supervisor configuration
        modelBuilder.Entity<Supervisor>()
            .HasOne(s => s.Application)
            .WithMany(app => app.Supervisors)
            .HasForeignKey(s => s.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // CollaborationSession configuration
        modelBuilder.Entity<CollaborationSession>()
            .HasIndex(cs => cs.CollaborationId)
            .IsUnique();

        modelBuilder.Entity<CollaborationSession>()
            .HasOne(cs => cs.User)
            .WithMany(u => u.CollaborationSessions)
            .HasForeignKey(cs => cs.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<CollaborationSession>()
            .HasOne(cs => cs.Agent)
            .WithMany(a => a.CollaborationSessions)
            .HasForeignKey(cs => cs.AgentId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<CollaborationSession>()
            .HasOne(cs => cs.Application)
            .WithMany(app => app.CollaborationSessions)
            .HasForeignKey(cs => cs.ApplicationId)
            .OnDelete(DeleteBehavior.Cascade);

        // ChatMessage configuration
        modelBuilder.Entity<ChatMessage>()
            .HasOne(cm => cm.CollaborationSession)
            .WithMany(cs => cs.ChatMessages)
            .HasForeignKey(cm => cm.CollaborationSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        // AgentSupervisorSession configuration
        modelBuilder.Entity<AgentSupervisorSession>()
            .HasKey(ass => new { ass.CollaborationSessionId, ass.AgentId, ass.SupervisorId });

        modelBuilder.Entity<AgentSupervisorSession>()
            .HasOne(ass => ass.CollaborationSession)
            .WithMany(cs => cs.SupervisorSessions)
            .HasForeignKey(ass => ass.CollaborationSessionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentSupervisorSession>()
            .HasOne(ass => ass.Agent)
            .WithMany(a => a.SupervisorSessions)
            .HasForeignKey(ass => ass.AgentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<AgentSupervisorSession>()
            .HasOne(ass => ass.Supervisor)
            .WithMany(s => s.AgentSessions)
            .HasForeignKey(ass => ass.SupervisorId)
            .OnDelete(DeleteBehavior.Cascade);

        // Seed data
        SeedData(modelBuilder);
    }

    private void SeedData(ModelBuilder modelBuilder)
    {
        // Add default application
        modelBuilder.Entity<Application>().HasData(
            new Application
            {
                Id = 1,
                Name = "Default Application",
                Description = "Default application for collaboration engine",
                ApiKey = "default-api-key-12345",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
    }
}
