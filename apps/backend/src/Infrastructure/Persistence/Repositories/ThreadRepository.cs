using AgentFrameworkQuickStart.Core.Interfaces;
using AgentFrameworkQuickStart.Models;
using Microsoft.EntityFrameworkCore;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence.Repositories;

/// <summary>
/// Implementation of IThreadRepository using Entity Framework Core
/// </summary>
public class ThreadRepository : IThreadRepository
{
    private readonly AppDbContext _context;

    public ThreadRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<IEnumerable<ChatThread>> GetAllAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Threads.AsQueryable();

        if (!includeArchived)
        {
            query = query.Where(t => !t.IsArchived);
        }

        return await query.OrderByDescending(t => t.UpdatedAt).ToListAsync(cancellationToken);
    }

    public async Task<ChatThread?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.Threads.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ChatThread?> GetByIdWithMessagesAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .Threads.Include(t => t.Messages.OrderBy(m => m.SequenceNumber))
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<ChatThread> CreateAsync(
        ChatThread thread,
        CancellationToken cancellationToken = default
    )
    {
        thread.Id = thread.Id == Guid.Empty ? Guid.NewGuid() : thread.Id;
        thread.CreatedAt = DateTime.UtcNow;
        thread.UpdatedAt = DateTime.UtcNow;

        _context.Threads.Add(thread);
        await _context.SaveChangesAsync(cancellationToken);

        return thread;
    }

    public async Task<ChatThread> UpdateAsync(
        ChatThread thread,
        CancellationToken cancellationToken = default
    )
    {
        thread.UpdatedAt = DateTime.UtcNow;

        _context.Threads.Update(thread);
        await _context.SaveChangesAsync(cancellationToken);

        return thread;
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var thread = await _context.Threads.FindAsync([id], cancellationToken);

        if (thread != null)
        {
            _context.Threads.Remove(thread);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var thread = await _context.Threads.FindAsync([id], cancellationToken);

        if (thread != null)
        {
            thread.IsArchived = true;
            thread.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<IEnumerable<ChatThread>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default
    )
    {
        var searchTerm = query.ToLower();

        return await _context
            .Threads.Where(t => !t.IsArchived)
            .Where(t =>
                t.Title.ToLower().Contains(searchTerm)
                || t.Messages.Any(m => m.Content.ToLower().Contains(searchTerm))
            )
            .OrderByDescending(t => t.UpdatedAt)
            .ToListAsync(cancellationToken);
    }
}
