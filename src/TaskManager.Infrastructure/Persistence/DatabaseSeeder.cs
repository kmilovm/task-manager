using Microsoft.EntityFrameworkCore;
using TaskManager.Application.Abstractions;
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
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        _context.Users.AddRange(
            User.Register("ada@example.com", "Ada Lovelace", _hasher.Hash(DemoPassword), _clock.UtcNow),
            User.Register("grace@example.com", "Grace Hopper", _hasher.Hash(DemoPassword), _clock.UtcNow));

        await _context.SaveChangesAsync(cancellationToken);
    }
}
