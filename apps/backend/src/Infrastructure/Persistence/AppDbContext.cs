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
    public DbSet<ConversationContextEntity> ConversationContexts =>
        Set<ConversationContextEntity>();

    // Long-term memory tables
    public DbSet<UserMemoryEntity> UserMemories => Set<UserMemoryEntity>();
    public DbSet<ConversationSummaryEntity> ConversationSummaries =>
        Set<ConversationSummaryEntity>();

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

            // Optional relationship with ChatThread via ThreadId (not ConversationId)
            entity
                .HasOne(e => e.Thread)
                .WithMany()
                .HasForeignKey(e => e.ThreadId)
                .IsRequired(false)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure ConversationContextEntity
        modelBuilder.Entity<ConversationContextEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ConversationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.UserExpertiseLevel).HasMaxLength(50);
            entity.Property(e => e.PreferredLanguage).HasMaxLength(10);
            entity.Property(e => e.InferredRiskTolerance).HasMaxLength(50);

            // JSON columns
            entity.Property(e => e.EntitiesJson).HasColumnType("TEXT");
            entity.Property(e => e.GoalsJson).HasColumnType("TEXT");
            entity.Property(e => e.DecisionsJson).HasColumnType("TEXT");
            entity.Property(e => e.SubAgentFindingsJson).HasColumnType("TEXT");
            entity.Property(e => e.PendingActionsJson).HasColumnType("TEXT");
            entity.Property(e => e.ActiveConstraintsJson).HasColumnType("TEXT");

            // Indexes
            entity.HasIndex(e => e.ConversationId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.LastActivityAt);

            // Optional relationship with ChatThread
            entity
                .HasOne(e => e.Thread)
                .WithMany()
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Configure UserMemoryEntity
        modelBuilder.Entity<UserMemoryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.UserId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.PreferredLanguage).HasMaxLength(10);
            entity.Property(e => e.ExpertiseLevel).HasMaxLength(20);
            entity.Property(e => e.RiskTolerance).HasMaxLength(20);

            // JSON columns
            entity.Property(e => e.FrequentAccountsJson).HasColumnType("TEXT");
            entity.Property(e => e.FrequentPortfoliosJson).HasColumnType("TEXT");
            entity.Property(e => e.FrequentFundsJson).HasColumnType("TEXT");
            entity.Property(e => e.PreferencesJson).HasColumnType("TEXT");
            entity.Property(e => e.FactsJson).HasColumnType("TEXT");
            entity.Property(e => e.InterestsJson).HasColumnType("TEXT");
            entity.Property(e => e.TopicFrequencyJson).HasColumnType("TEXT");
            entity.Property(e => e.CommunicationStyleJson).HasColumnType("TEXT");

            // Indexes
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasIndex(e => e.UpdatedAt);
        });

        // Configure ConversationSummaryEntity
        modelBuilder.Entity<ConversationSummaryEntity>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.ConversationId).IsRequired().HasMaxLength(100);
            entity.Property(e => e.UserId).HasMaxLength(100);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(500);
            entity.Property(e => e.Summary).IsRequired();
            entity.Property(e => e.SatisfactionIndicator).HasMaxLength(50);

            // JSON columns
            entity.Property(e => e.TopicsJson).HasColumnType("TEXT");
            entity.Property(e => e.EntitiesJson).HasColumnType("TEXT");
            entity.Property(e => e.ActionsJson).HasColumnType("TEXT");
            entity.Property(e => e.DecisionsJson).HasColumnType("TEXT");
            entity.Property(e => e.PendingFollowUpsJson).HasColumnType("TEXT");
            entity.Property(e => e.SubAgentsUsedJson).HasColumnType("TEXT");
            entity.Property(e => e.MultiModalContentJson).HasColumnType("TEXT");
            entity.Property(e => e.KeywordsJson).HasColumnType("TEXT");

            // Indexes
            entity.HasIndex(e => e.ConversationId).IsUnique();
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.ImportanceScore);
            entity.HasIndex(e => e.StartedAt);
            entity.HasIndex(e => e.CreatedAt);

            // Optional relationship with ChatThread
            entity
                .HasOne(e => e.Thread)
                .WithMany()
                .HasForeignKey(e => e.ThreadId)
                .OnDelete(DeleteBehavior.SetNull);
        });
    }
}
