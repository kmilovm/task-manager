using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Shouldly;
using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;
using TaskManager.Infrastructure.Persistence;
using TaskManager.Infrastructure.Persistence.Repositories;
using Xunit.Abstractions;

namespace TaskManager.Infrastructure.Tests.Tasks;

public sealed class TaskRepositoryTests : IDisposable
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private readonly SqliteDatabase _database = new();
    private readonly ITestOutputHelper _output;
    private readonly Guid _ada;
    private readonly Guid _grace;

    public TaskRepositoryTests(ITestOutputHelper output)
    {
        _output = output;

        var ada = User.Register("ada@example.com", "Ada Lovelace", "hash", Now);
        var grace = User.Register("grace@example.com", "Grace Hopper", "hash", Now);

        using var context = _database.CreateContext();
        context.Users.AddRange(ada, grace);
        context.SaveChanges();

        _ada = ada.Id;
        _grace = grace.Id;
    }

    [Fact]
    public async Task AddAsync_PersistsTheTask()
    {
        var task = TaskItem.Create("Prepare invoices", "Q1 batch", new DateOnly(2030, 3, 31), _ada, Now);

        await using (var context = _database.CreateContext())
        {
            await new TaskRepository(context).AddAsync(task);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Tasks.SingleAsync();

            stored.Id.ShouldBe(task.Id);
            stored.Title.ShouldBe("Prepare invoices");
            stored.Description.ShouldBe("Q1 batch");
            stored.Status.ShouldBe(TaskItemStatus.Pending);
            stored.DueDate.ShouldBe(new DateOnly(2030, 3, 31));
            stored.CreatedAt.ShouldBe(Now);
            stored.CompletedAt.ShouldBeNull();
            stored.OwnerId.ShouldBe(_ada);
        }
    }

    [Fact]
    public async Task AddAsync_RoundTripsATaskWithoutADueDateOrACompletionTime()
    {
        var task = TaskItem.Create("Book the meeting room", null, null, _ada, Now);

        await using (var context = _database.CreateContext())
        {
            await new TaskRepository(context).AddAsync(task);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Tasks.SingleAsync();

            stored.DueDate.ShouldBeNull();
            stored.CompletedAt.ShouldBeNull();
            stored.Description.ShouldBeNull();
        }
    }

    [Fact]
    public async Task AddAsync_RoundTripsACompletedTask()
    {
        var completedAt = Now.AddHours(6);
        var task = TaskItem.Create("Archive last sprint", null, null, _ada, Now);
        task.ChangeStatus(TaskItemStatus.Done, completedAt);

        await using (var context = _database.CreateContext())
        {
            await new TaskRepository(context).AddAsync(task);
        }

        await using (var context = _database.CreateContext())
        {
            var stored = await context.Tasks.SingleAsync();

            stored.Status.ShouldBe(TaskItemStatus.Done);
            stored.CompletedAt.ShouldBe(completedAt);
        }
    }

    [Fact]
    public async Task AddAsync_WithAnOwnerThatDoesNotExist_IsRejectedByTheForeignKey()
    {
        var orphan = TaskItem.Create("Orphaned", null, null, Guid.NewGuid(), Now);

        await using var context = _database.CreateContext();

        await Should.ThrowAsync<DbUpdateException>(() => new TaskRepository(context).AddAsync(orphan));
    }

    [Fact]
    public async Task GetByIdAsync_ReturnsTheTaskWhoeverOwnsIt()
    {
        var mine = await GivenTask(_ada, "Write the report");
        var hers = await GivenTask(_grace, "Quarterly forecast");

        await using var context = _database.CreateContext();
        var repository = new TaskRepository(context);

        (await repository.GetByIdAsync(mine.Id))!.Title.ShouldBe("Write the report");
        (await repository.GetByIdAsync(hers.Id))!.Title.ShouldBe("Quarterly forecast");
    }

    [Fact]
    public async Task GetByIdAsync_WhenNoTaskMatches_ReturnsNull()
    {
        await using var context = _database.CreateContext();

        (await new TaskRepository(context).GetByIdAsync(Guid.NewGuid())).ShouldBeNull();
    }

    [Fact]
    public async Task ListAsync_ReturnsOnlyTheTasksOfTheGivenOwner()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck");
        await GivenTask(_grace, "Quarterly forecast");

        var mine = await List(_ada);

        mine.Count.ShouldBe(2);
        mine.Select(task => task.Title).ShouldNotContain("Quarterly forecast");
    }

    [Fact]
    public async Task ListAsync_WhenTheOwnerHasNoTasks_ReturnsAnEmptyList()
    {
        await GivenTask(_grace, "Quarterly forecast");

        (await List(_ada)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ListAsync_OrdersByDueDateThenCreatedAt()
    {
        await GivenTask(_ada, "Review the deck", dueDate: new DateOnly(2030, 1, 5), createdAt: Now);
        await GivenTask(_ada, "Call the supplier", dueDate: new DateOnly(2030, 1, 5), createdAt: Now.AddHours(2));
        await GivenTask(_ada, "Write the report", dueDate: new DateOnly(2030, 1, 10), createdAt: Now);
        await GivenTask(_ada, "Archive last sprint", createdAt: Now);
        await GivenTask(_ada, "Book the meeting room", createdAt: Now.AddHours(1));

        var mine = await List(_ada);

        mine.Select(task => task.Title).ShouldBe([
            "Call the supplier",
            "Review the deck",
            "Write the report",
            "Book the meeting room",
            "Archive last sprint",
        ]);
    }

    [Fact]
    public async Task ListAsync_FiltersByStatus()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck", status: TaskItemStatus.InProgress);
        await GivenTask(_ada, "Archive last sprint", status: TaskItemStatus.Done);

        var inProgress = await List(_ada, status: TaskItemStatus.InProgress);

        inProgress.Count.ShouldBe(1);
        inProgress[0].Title.ShouldBe("Review the deck");
    }

    [Fact]
    public async Task ListAsync_FiltersBySearchTerm()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck");

        var found = await List(_ada, search: "report");

        found.Count.ShouldBe(1);
        found[0].Title.ShouldBe("Write the report");
    }

    [Theory]
    [InlineData("REPORT")]
    [InlineData("report")]
    [InlineData("RePoRt")]
    public async Task ListAsync_SearchIsCaseInsensitive(string search)
    {
        await GivenTask(_ada, "Write the Report");
        await GivenTask(_ada, "Review the deck");

        var found = await List(_ada, search: search);

        found.Count.ShouldBe(1);
        found[0].Title.ShouldBe("Write the Report");
    }

    [Fact]
    public async Task ListAsync_SearchMatchesPartOfTheTitleButNotTheDescription()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck", description: "the quarterly report");

        var found = await List(_ada, search: "report");

        found.Count.ShouldBe(1);
        found[0].Title.ShouldBe("Write the report");
    }

    [Fact]
    public async Task ListAsync_CombinesTheStatusFilterAndTheSearchTermWithAnd()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Write the annual report", status: TaskItemStatus.InProgress);
        await GivenTask(_ada, "Review the deck", status: TaskItemStatus.InProgress);

        var found = await List(_ada, status: TaskItemStatus.InProgress, search: "report");

        found.Count.ShouldBe(1);
        found[0].Title.ShouldBe("Write the annual report");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ListAsync_WithoutASearchTerm_DoesNotFilter(string? search)
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck");

        (await List(_ada, search: search)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_IgnoresSurroundingWhitespaceInTheSearchTerm()
    {
        await GivenTask(_ada, "Write the report");
        await GivenTask(_ada, "Review the deck");

        (await List(_ada, search: "  report  ")).Count.ShouldBe(1);
    }

    [Fact]
    public async Task ListAsync_FiltersSearchesAndOrdersInASingleSqlStatement()
    {
        await GivenTask(_ada, "Write the report", dueDate: new DateOnly(2030, 1, 10));
        await GivenTask(_ada, "Archive last sprint");

        var unfiltered = new List<string>();
        var filtered = new List<string>();

        await using (var context = LoggingContext(unfiltered))
        {
            await new TaskRepository(context).ListAsync(_ada, null, null);
        }

        await using (var context = LoggingContext(filtered))
        {
            await new TaskRepository(context).ListAsync(_ada, TaskItemStatus.Pending, "REPORT");
        }

        _output.WriteLine(string.Join(Environment.NewLine, unfiltered.Concat(filtered)));

        unfiltered.Count.ShouldBe(1);
        unfiltered[0].ShouldContain("ORDER BY", Case.Insensitive);

        filtered.Count.ShouldBe(1);
        filtered[0].ShouldContain("ORDER BY", Case.Insensitive);
        filtered[0].ShouldContain("lower(", Case.Insensitive);
        filtered[0].ShouldContain("WHERE", Case.Insensitive);
    }

    [Fact]
    public async Task UpdateAsync_PersistsTheChanges()
    {
        var task = await GivenTask(_ada, "Write the report", dueDate: new DateOnly(2030, 1, 10));

        await using (var context = _database.CreateContext())
        {
            var repository = new TaskRepository(context);
            var stored = await repository.GetByIdAsync(task.Id);

            stored!.Update("Write the annual report", "With the Q4 figures", null);
            stored.ChangeStatus(TaskItemStatus.Done, Now.AddHours(6));

            await repository.UpdateAsync(stored);
        }

        await using (var context = _database.CreateContext())
        {
            var reloaded = await context.Tasks.SingleAsync();

            reloaded.Title.ShouldBe("Write the annual report");
            reloaded.Description.ShouldBe("With the Q4 figures");
            reloaded.DueDate.ShouldBeNull();
            reloaded.Status.ShouldBe(TaskItemStatus.Done);
            reloaded.CompletedAt.ShouldBe(Now.AddHours(6));
        }
    }

    [Fact]
    public async Task UpdateAsync_PersistsATaskLoadedByAnotherContext()
    {
        var task = await GivenTask(_ada, "Write the report");

        TaskItem detached;

        await using (var context = _database.CreateContext())
        {
            detached = (await new TaskRepository(context).GetByIdAsync(task.Id))!;
        }

        detached.Update("Write the annual report", null, null);

        await using (var context = _database.CreateContext())
        {
            await new TaskRepository(context).UpdateAsync(detached);
        }

        await using (var reader = _database.CreateContext())
        {
            (await reader.Tasks.SingleAsync()).Title.ShouldBe("Write the annual report");
        }
    }

    [Fact]
    public async Task DeleteAsync_RemovesTheTaskPermanently()
    {
        var task = await GivenTask(_ada, "Review the deck");
        await GivenTask(_ada, "Write the report");

        await using (var context = _database.CreateContext())
        {
            var repository = new TaskRepository(context);

            await repository.DeleteAsync((await repository.GetByIdAsync(task.Id))!);
        }

        await using (var context = _database.CreateContext())
        {
            (await context.Tasks.AnyAsync(stored => stored.Id == task.Id)).ShouldBeFalse();
            (await context.Tasks.CountAsync()).ShouldBe(1);
        }
    }

    public void Dispose() => _database.Dispose();

    private AppDbContext LoggingContext(ICollection<string> statements) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_database.CreateContext().Database.GetDbConnection())
            .LogTo(statements.Add, [RelationalEventId.CommandExecuted])
            .Options);

    private async Task<IReadOnlyList<TaskItem>> List(
        Guid ownerId,
        TaskItemStatus? status = null,
        string? search = null)
    {
        await using var context = _database.CreateContext();

        return await new TaskRepository(context).ListAsync(ownerId, status, search);
    }

    private async Task<TaskItem> GivenTask(
        Guid ownerId,
        string title,
        string? description = null,
        DateOnly? dueDate = null,
        TaskItemStatus status = TaskItemStatus.Pending,
        DateTimeOffset? createdAt = null)
    {
        var task = TaskItem.Create(title, description, dueDate, ownerId, createdAt ?? Now);

        if (status != TaskItemStatus.Pending)
        {
            task.ChangeStatus(status, (createdAt ?? Now).AddHours(6));
        }

        await using var context = _database.CreateContext();
        await new TaskRepository(context).AddAsync(task);

        return task;
    }
}
