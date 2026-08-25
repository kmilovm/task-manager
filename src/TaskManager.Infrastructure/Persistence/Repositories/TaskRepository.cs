using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Tasks;

namespace TaskManager.Infrastructure.Persistence.Repositories;

public sealed class TaskRepository : ITaskRepository
{
    private readonly AppDbContext _context;

    public TaskRepository(AppDbContext context) => _context = context;

    public Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        _context.Tasks.FirstOrDefaultAsync(task => task.Id == id, cancellationToken);

    public async Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid ownerId,
        TaskItemStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Tasks.Where(task => task.OwnerId == ownerId);

        if (status is { } wanted)
        {
            query = query.Where(task => task.Status == wanted);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Lowering both sides keeps the match case-insensitive on every provider instead of
            // inheriting whatever collation the database happens to be configured with.
            var term = search.Trim().ToLowerInvariant();

            // CA1304/CA1311/CA1862 assume this runs in memory. It does not: the expression is
            // translated to SQL, where ToLower() becomes LOWER() and Contains() becomes a literal
            // substring test. The culture-aware and StringComparison overloads the rules ask for
            // have no SQL translation, so taking their advice would break the query instead.
#pragma warning disable CA1304, CA1311, CA1862
            query = query.Where(task => task.Title.ToLower().Contains(term));
#pragma warning restore CA1304, CA1311, CA1862
        }

        return await query
            .OrderBy(task => task.DueDate == null)
            .ThenBy(task => task.DueDate)
            .ThenByDescending(task => task.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _context.Tasks.Add(task);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _context.Tasks.Update(task);

        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(TaskItem task, CancellationToken cancellationToken = default)
    {
        _context.Tasks.Remove(task);

        await _context.SaveChangesAsync(cancellationToken);
    }
}
