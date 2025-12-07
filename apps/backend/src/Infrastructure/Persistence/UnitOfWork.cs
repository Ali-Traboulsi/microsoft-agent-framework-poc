using AgentFrameworkQuickStart.Core.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace AgentFrameworkQuickStart.Infrastructure.Persistence;

/// <summary>
/// Implementation of Unit of Work pattern
/// </summary>
public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private IDbContextTransaction? _transaction;

    private IThreadRepository? _threads;
    private IMessageRepository? _messages;

    public UnitOfWork(AppDbContext context)
    {
        _context = context;
    }

    public IThreadRepository Threads => _threads ??= new Repositories.ThreadRepository(_context);

    public IMessageRepository Messages =>
        _messages ??= new Repositories.MessageRepository(_context);

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        _transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.CommitAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackTransactionAsync(CancellationToken cancellationToken = default)
    {
        if (_transaction != null)
        {
            await _transaction.RollbackAsync(cancellationToken);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _context.Dispose();
    }
}
