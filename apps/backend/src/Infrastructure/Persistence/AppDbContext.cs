using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence;

/// <summary>
/// Application database context using Entity Framework Core
/// </summary>
public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<ChatThread> Threads => Set<ChatThread>();
    public DbSet<ChatMessage> Messages => Set<ChatMessage>();
    public DbSet<ConversationMemoryEntry> ConversationMemoryEntries =>
        Set<ConversationMemoryEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configure ChatThread
        modelBuilder.Entity<ChatThread>(entity =>
        {
            entity.HasKey(t => t.Id);

            entity.Property(t => t.Title).IsRequired().HasMaxLength(500);

            entity.Property(t => t.Summary).HasMaxLength(2000);

            entity.HasIndex(t => t.CreatedAt);
            entity.HasIndex(t => t.UpdatedAt);
            entity.HasIndex(t => t.IsArchived);

            // One-to-many relationship with messages
            entity
                .HasMany(t => t.Messages)
                .WithOne(m => m.Thread)
                .HasForeignKey(m => m.ThreadId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        // Configure ChatMessage
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.HasKey(m => m.Id);

            entity.Property(m => m.Content).IsRequired();

            entity.Property(m => m.Role).IsRequired().HasConversion<string>();

            entity.Property(m => m.SubAgentName).HasMaxLength(100);

            entity.HasIndex(m => m.ThreadId);
            entity.HasIndex(m => m.Timestamp);
            entity.HasIndex(m => m.SequenceNumber);

            // Composite index for efficient thread message retrieval
            entity.HasIndex(m => new { m.ThreadId, m.SequenceNumber });
        });

        // Configure ConversationMemoryEntry
        modelBuilder.Entity<ConversationMemoryEntry>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.AgentName).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserRequest).IsRequired();
            entity.Property(e => e.AgentResponse).IsRequired();

            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.ConversationId, e.SequenceNumber });

            // Optional relationship with ChatThread
            entity
                .HasOne(e => e.Thread)
                .WithMany()
                .HasForeignKey(e => e.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
