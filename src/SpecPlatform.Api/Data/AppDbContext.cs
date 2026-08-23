using Microsoft.EntityFrameworkCore;
using SpecPlatform.Shared.Models;

namespace SpecPlatform.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Spec> Specs => Set<Spec>();
    public DbSet<SpecVersion> SpecVersions => Set<SpecVersion>();
    public DbSet<AcceptanceCriterion> AcceptanceCriteria => Set<AcceptanceCriterion>();
    public DbSet<ScopeTag> ScopeTags => Set<ScopeTag>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessageRecord> ChatMessages => Set<ChatMessageRecord>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>()
            .HasMany(p => p.Specs)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Spec>()
            .HasMany(s => s.Versions)
            .WithOne(v => v.Spec)
            .HasForeignKey(v => v.SpecId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecVersion>()
            .HasMany(v => v.AcceptanceCriteria)
            .WithOne()
            .HasForeignKey(a => a.SpecVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<SpecVersion>()
            .HasMany(v => v.ScopeTags)
            .WithOne()
            .HasForeignKey(t => t.SpecVersionId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ChatSession>()
            .HasMany(cs => cs.Messages)
            .WithOne(cm => cm.ChatSession)
            .HasForeignKey(cm => cm.ChatSessionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
