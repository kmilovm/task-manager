using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;

namespace TaskManager.Infrastructure.Persistence;

public sealed class DatabaseSeeder
{
    public const string DemoPassword = "Passw0rd!";

    private readonly AppDbContext _context;
    private readonly IPasswordHasher _hasher;
    private readonly IClock _clock;

    public DatabaseSeeder(AppDbContext context, IPasswordHasher hasher, IClock clock)
    {
        _context = context;
        _hasher = hasher;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await SeedUsersAsync(cancellationToken);
        await SeedTasksAsync(cancellationToken);
    }

    private async Task SeedUsersAsync(CancellationToken cancellationToken)
    {
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        _context.Users.AddRange(
            User.Register("ada@example.com", "Ada Lovelace", _hasher.Hash(DemoPassword), _clock.UtcNow),
            User.Register("grace@example.com", "Grace Hopper", _hasher.Hash(DemoPassword), _clock.UtcNow));

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task SeedTasksAsync(CancellationToken cancellationToken)
    {
        if (await _context.Tasks.AnyAsync(cancellationToken))
        {
            return;
        }

        var ada = await FindAsync("ada@example.com", cancellationToken);
        var grace = await FindAsync("grace@example.com", cancellationToken);

        if (ada is null || grace is null)
        {
            return;
        }

        var now = _clock.UtcNow;
        var nextMonth = DateOnly.FromDateTime(now.UtcDateTime).AddMonths(1);

        var archived = TaskItem.Create("Archive last sprint", null, null, ada.Id, now);
        archived.ChangeStatus(TaskItemStatus.Done, now);

        var deck = TaskItem.Create("Review the deck", "Slides for the steering committee", nextMonth, ada.Id, now);
        deck.ChangeStatus(TaskItemStatus.InProgress, now);

        _context.Tasks.AddRange(
            TaskItem.Create("Write the report", "Quarterly numbers", nextMonth.AddDays(5), ada.Id, now),
            deck,
            archived,
            TaskItem.Create("Quarterly forecast", null, nextMonth, grace.Id, now));

        await _context.SaveChangesAsync(cancellationToken);
    }

    private Task<User?> FindAsync(string email, CancellationToken cancellationToken)
    {
        var address = Email.Create(email);

        return _context.Users.FirstOrDefaultAsync(user => user.Email == address, cancellationToken);
    }
}
