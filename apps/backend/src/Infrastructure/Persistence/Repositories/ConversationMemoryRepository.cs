using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Repository for managing ConversationMemoryEntry entities
/// </summary>
public interface IConversationMemoryRepository
{
    Task<List<ConversationMemoryEntry>> GetByConversationIdAsync(Guid conversationId);
    Task<int> GetNextSequenceNumberAsync(Guid conversationId);
    Task AddAsync(ConversationMemoryEntry entry);
    Task DeleteByConversationIdAsync(Guid conversationId);
}

public class ConversationMemoryRepository(AppDbContext context) : IConversationMemoryRepository
{
    private readonly AppDbContext _context = context;

    public async Task<List<ConversationMemoryEntry>> GetByConversationIdAsync(Guid conversationId)
    {
        return await _context
            .ConversationMemoryEntries.Where(e => e.ConversationId == conversationId)
            .OrderBy(e => e.SequenceNumber)
            .ToListAsync();
    }

    public async Task<int> GetNextSequenceNumberAsync(Guid conversationId)
    {
        var maxSequence =
            await _context
                .ConversationMemoryEntries.Where(e => e.ConversationId == conversationId)
                .MaxAsync(e => (int?)e.SequenceNumber) ?? 0;

        return maxSequence + 1;
    }

    public async Task AddAsync(ConversationMemoryEntry entry)
    {
        await _context.ConversationMemoryEntries.AddAsync(entry);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteByConversationIdAsync(Guid conversationId)
    {
        var entries = await _context
            .ConversationMemoryEntries.Where(e => e.ConversationId == conversationId)
            .ToListAsync();

        _context.ConversationMemoryEntries.RemoveRange(entries);
        await _context.SaveChangesAsync();
    }
}
