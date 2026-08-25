using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Persistence;

namespace TaskManager.Infrastructure.Tests.Persistence;

public sealed class DatabaseSeederTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly DateOnly Today = new(2026, 1, 15);

    private readonly SqliteDatabase _database = new();
    private readonly IPasswordHasher _hasher = Substitute.For<IPasswordHasher>();
    private readonly IClock _clock = Substitute.For<IClock>();

    public DatabaseSeederTests()
    {
        _clock.UtcNow.Returns(Now);
        _hasher.Hash(Arg.Any<string>()).Returns(call => $"hashed:{call.Arg<string>()}");
    }

    [Fact]
    public async Task SeedAsync_OnAnEmptyDatabase_CreatesTheDemoAccounts()
    {
        await Seed();

        await using var context = _database.CreateContext();
        var users = await context.Users.ToListAsync();

        users.Count.ShouldBe(2);
        users.Select(user => user.Email.Value)
            .ShouldBe(["ada@example.com", "grace@example.com"], ignoreOrder: true);
        users.Select(user => user.DisplayName)
            .ShouldBe(["Ada Lovelace", "Grace Hopper"], ignoreOrder: true);
    }

    [Fact]
    public async Task SeedAsync_StoresAHashRatherThanTheDemoPassword()
    {
        await Seed();

        await using var context = _database.CreateContext();

        foreach (var user in await context.Users.ToListAsync())
        {
            user.PasswordHash.ShouldBe($"hashed:{DatabaseSeeder.DemoPassword}");
            user.PasswordHash.ShouldNotBe(DatabaseSeeder.DemoPassword);
        }
    }

    [Fact]
    public async Task SeedAsync_OnAnEmptyDatabase_CreatesTheDemoTasks()
    {
        await Seed();

        await using var context = _database.CreateContext();
        var tasks = await context.Tasks.ToListAsync();

        tasks.Count.ShouldBe(4);
        (await TasksOf(context, "ada@example.com")).Count.ShouldBe(3);
        (await TasksOf(context, "grace@example.com")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task SeedAsync_CoversEveryStatusSoTheDemoListIsWorthLookingAt()
    {
        await Seed();

        await using var context = _database.CreateContext();
        var mine = await TasksOf(context, "ada@example.com");

        mine.Select(task => task.Status)
            .ShouldBe([TaskItemStatus.Pending, TaskItemStatus.InProgress, TaskItemStatus.Done], ignoreOrder: true);

        mine.Single(task => task.Status == TaskItemStatus.Done).CompletedAt.ShouldBe(Now);
        mine.Where(task => task.Status != TaskItemStatus.Done)
            .ShouldAllBe(task => task.CompletedAt == null);
    }

    [Fact]
    public async Task SeedAsync_LeavesOneTaskUndatedSoTheOrderingIsVisible()
    {
        await Seed();

        await using var context = _database.CreateContext();
        var mine = await TasksOf(context, "ada@example.com");

        mine.Count(task => task.DueDate is null).ShouldBe(1);
        mine.Select(task => task.DueDate).Distinct().Count().ShouldBe(mine.Count);
    }

    [Fact]
    public async Task SeedAsync_NeverDatesATaskInThePast()
    {
        await Seed();

        await using var context = _database.CreateContext();

        foreach (var task in await context.Tasks.ToListAsync())
        {
            if (task.DueDate is { } due)
            {
                due.ShouldBeGreaterThanOrEqualTo(Today);
            }

            task.CreatedAt.ShouldBe(Now);
        }
    }

    [Fact]
    public async Task SeedAsync_RunTwice_DoesNotDuplicateAnything()
    {
        await Seed();
        await Seed();

        await using var context = _database.CreateContext();

        (await context.Users.CountAsync()).ShouldBe(2);
        (await context.Tasks.CountAsync()).ShouldBe(4);
    }

    [Fact]
    public async Task SeedAsync_WhenTheAccountsExistButTheTasksDoNot_StillSeedsTheTasks()
    {
        await using (var context = _database.CreateContext())
        {
            context.Users.AddRange(
                User.Register("ada@example.com", "Ada Lovelace", "hash", Now),
                User.Register("grace@example.com", "Grace Hopper", "hash", Now));

            await context.SaveChangesAsync();
        }

        await Seed();

        await using var reader = _database.CreateContext();

        (await reader.Users.CountAsync()).ShouldBe(2);
        (await reader.Tasks.CountAsync()).ShouldBe(4);
    }

    [Fact]
    public async Task SeedAsync_WhenSomeTasksAlreadyExist_AddsNoMore()
    {
        await Seed();

        await using (var context = _database.CreateContext())
        {
            await context.Tasks.Where(task => task.Title != "Write the report").ExecuteDeleteAsync();
        }

        await Seed();

        await using var reader = _database.CreateContext();

        (await reader.Tasks.CountAsync()).ShouldBe(1);
    }

    public void Dispose() => _database.Dispose();

    private static async Task<IReadOnlyList<TaskItem>> TasksOf(AppDbContext context, string email)
    {
        var users = await context.Users.ToListAsync();
        var owner = users.Single(user => user.Email.Value == email);

        return await context.Tasks.Where(task => task.OwnerId == owner.Id).ToListAsync();
    }

    private async Task Seed()
    {
        await using var context = _database.CreateContext();

        await new DatabaseSeeder(context, _hasher, _clock).SeedAsync();
    }
}
