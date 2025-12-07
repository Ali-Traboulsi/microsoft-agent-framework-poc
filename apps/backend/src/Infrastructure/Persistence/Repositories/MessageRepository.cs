using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation of IMessageRepository using Entity Framework Core
/// </summary>
public class MessageRepository : IMessageRepository
{
    private readonly AppDbContext _context;

    public MessageRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ChatMessage>> GetByThreadIdAsync(
        Guid threadId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Messages.Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.SequenceNumber)
            .ToListAsync(cancellationToken);
    }

    public async Task<ChatMessage?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Messages.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public async Task<ChatMessage> AddAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default
    )
    {
        message.Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id;
        message.Timestamp = DateTime.UtcNow;

        if (message.SequenceNumber == 0)
        {
            message.SequenceNumber = await GetNextSequenceNumberAsync(
                message.ThreadId,
                cancellationToken
            );
        }

        _context.Messages.Add(message);
        await _context.SaveChangesAsync(cancellationToken);

        // Update the thread's UpdatedAt timestamp
        var thread = await _context.Threads.FindAsync([message.ThreadId], cancellationToken);
        if (thread != null)
        {
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return message;
    }

    public async Task<IEnumerable<ChatMessage>> AddRangeAsync(
        IEnumerable<ChatMessage> messages,
        CancellationToken cancellationToken = default
    )
    {
        var messageList = messages.ToList();

        if (!messageList.Any())
            return messageList;

        var threadId = messageList.First().ThreadId;
        var nextSequence = await GetNextSequenceNumberAsync(threadId, cancellationToken);

        foreach (var message in messageList)
        {
            message.Id = message.Id == Guid.Empty ? Guid.NewGuid() : message.Id;
            message.Timestamp = DateTime.UtcNow;

            if (message.SequenceNumber == 0)
            {
                message.SequenceNumber = nextSequence++;
            }
        }

        _context.Messages.AddRange(messageList);
        await _context.SaveChangesAsync(cancellationToken);

        // Update the thread's UpdatedAt timestamp
        var thread = await _context.Threads.FindAsync([threadId], cancellationToken);
        if (thread != null)
        {
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }

        return messageList;
    }

    public async Task<ChatMessage> UpdateAsync(
        ChatMessage message,
        CancellationToken cancellationToken = default
    )
    {
        _context.Messages.Update(message);
        await _context.SaveChangesAsync(cancellationToken);

        return message;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var message = await _context.Messages.FindAsync([id], cancellationToken);

        if (message != null)
        {
            _context.Messages.Remove(message);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<int> GetNextSequenceNumberAsync(
        Guid threadId,
        CancellationToken cancellationToken = default
    )
    {
        var maxSequence = await _context
            .Messages.Where(m => m.ThreadId == threadId)
            .MaxAsync(m => (int?)m.SequenceNumber, cancellationToken);

        return (maxSequence ?? 0) + 1;
    }
}
