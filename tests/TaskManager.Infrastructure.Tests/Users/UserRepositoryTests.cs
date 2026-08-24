using Microsoft.EntityFrameworkCore;
using Shouldly;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Persistence.Repositories;

namespace TaskManager.Infrastructure.Tests.Users;

public sealed class UserRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly SqliteDatabase _database = new();

    [Fact]
    public async Task AddAsync_PersistsTheAccount()
    {
        var user = User.Register("ada@example.com", "Ada Lovelace", "hash", Now);

        await using (var context = _database.CreateContext())
        {
            await new UserRepository(context).AddAsync(user);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Users.SingleAsync();

            stored.Id.ShouldBe(user.Id);
            stored.Email.Value.ShouldBe("ada@example.com");
            stored.DisplayName.ShouldBe("Ada Lovelace");
            stored.PasswordHash.ShouldBe("hash");
            stored.CreatedAt.ShouldBe(Now);
        }
    }

    [Fact]
    public async Task AddAsync_WithAnEmailThatIsAlreadyTaken_IsRejectedByTheUniqueIndex()
    {
        await GivenAccount("ada@example.com");

        await using var context = _database.CreateContext();
        var repository = new UserRepository(context);

        await Should.ThrowAsync<DbUpdateException>(
            () => repository.AddAsync(User.Register("ada@example.com", "Ada Byron", "hash", Now)));
    }

    [Fact]
    public async Task GetByEmailAsync_FindsTheAccountRegardlessOfCasing()
    {
        await GivenAccount("ada@example.com");

        await using var context = _database.CreateContext();

        var found = await new UserRepository(context).GetByEmailAsync(Email.Create("ADA@Example.COM"));

        found.ShouldNotBeNull();
        found.DisplayName.ShouldBe("Ada Lovelace");
    }

    [Fact]
    public async Task GetByEmailAsync_WhenNoAccountMatches_ReturnsNull()
    {
        await using var context = _database.CreateContext();

        var found = await new UserRepository(context).GetByEmailAsync(Email.Create("nobody@example.com"));

        found.ShouldBeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheAccount()
    {
        var user = await GivenAccount("ada@example.com");

        await using var context = _database.CreateContext();

        var found = await new UserRepository(context).GetByIdAsync(user.Id);

        found.ShouldNotBeNull();
        found.Email.Value.ShouldBe("ada@example.com");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoAccountMatches_ReturnsNull()
    {
        await using var context = _database.CreateContext();

        (await new UserRepository(context).GetByIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Theory]
    [InlineData("ada@example.com", true)]
    [InlineData("nobody@example.com", false)]
    public async Task ExistsWithEmailAsync_ReportsWhetherTheAddressIsTaken(string email, bool expected)
    {
        await GivenAccount("ada@example.com");

        await using var context = _database.CreateContext();

        (await new UserRepository(context).ExistsWithEmailAsync(Email.Create(email))).ShouldBe(expected);
    }

    public void Dispose() => _database.Dispose();

    private async Task<User> GivenAccount(string email)
    {
        var user = User.Register(email, "Ada Lovelace", "hash", Now);

        await using var context = _database.CreateContext();
        await new UserRepository(context).AddAsync(user);

        return user;
    }
}
