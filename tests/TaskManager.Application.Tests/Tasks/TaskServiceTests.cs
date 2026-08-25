using FluentValidation;
using NSubstitute;
using Shouldly;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tests.Tasks;

public class TaskServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddHours(6);

    private static readonly Guid Ada = new("11111111-1111-1111-1111-111111111111");

    private static readonly Guid Grace = new("22222222-2222-2222-2222-222222222222");

    private readonly ITaskRepository _tasks = Substitute.For<ITaskRepository>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly TaskService _service;

    public TaskServiceTests()
    {
        _clock.UtcNow.Returns(Now);

        _service = new TaskService(
            _tasks,
            _clock,
            new CreateTaskRequestValidator(),
            new UpdateTaskRequestValidator());
    }

    [Fact]
    public async Task CreateAsync_WithFullPayload_PersistsTask()
    {
        var request = new CreateTaskRequest("  Prepare invoices  ", "  Q1 batch  ", new DateOnly(2030, 3, 31));

        var created = await _service.CreateAsync(Ada, request);

        await _tasks.Received(1).AddAsync(
            Arg.Is<TaskItem>(task =>
                task.Title == "Prepare invoices"
                && task.Description == "Q1 batch"
                && task.DueDate == new DateOnly(2030, 3, 31)
                && task.Status == TaskItemStatus.Pending
                && task.OwnerId == Ada
                && task.CreatedAt == Now
                && task.CompletedAt == null),
            Arg.Any<CancellationToken>());

        created.Id.ShouldNotBe(Guid.Empty);
        created.Title.ShouldBe("Prepare invoices");
        created.Description.ShouldBe("Q1 batch");
        created.DueDate.ShouldBe(new DateOnly(2030, 3, 31));
        created.Status.ShouldBe(TaskItemStatus.Pending);
        created.CreatedAt.ShouldBe(Now);
        created.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task CreateAsync_WithTitleOnly_StartsAsPendingWithoutADueDate()
    {
        var created = await _service.CreateAsync(Ada, new CreateTaskRequest("Book the meeting room", null, null));

        created.Status.ShouldBe(TaskItemStatus.Pending);
        created.DueDate.ShouldBeNull();
        created.Description.ShouldBeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateAsync_WithAnInvalidRequest_ThrowsValidationExceptionWithoutTouchingTheRepository(string title)
    {
        await Should.ThrowAsync<ValidationException>(
            () => _service.CreateAsync(Ada, new CreateTaskRequest(title, null, null)));

        await _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_WithAPastDueDate_ThrowsDomainExceptionWithoutTouchingTheRepository()
    {
        var exception = await Should.ThrowAsync<DomainException>(
            () => _service.CreateAsync(Ada, new CreateTaskRequest("Late task", null, new DateOnly(2020, 1, 1))));

        exception.Message.ShouldBe("Due date cannot be in the past.");
        await _tasks.DidNotReceive().AddAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyTasksOwnedByCaller()
    {
        GivenList(Ada, "Review the deck", "Write the report", "Archive last sprint");
        GivenList(Grace, "Quarterly forecast");

        var mine = await _service.GetAllAsync(Ada, null, null);

        mine.Count.ShouldBe(3);
        mine.Select(task => task.Title).ShouldNotContain("Quarterly forecast");
    }

    [Fact]
    public async Task GetAllAsync_PassesTheOwnerAndTheFiltersToTheRepositoryUnchanged()
    {
        GivenList(Ada, "Review the deck");

        await _service.GetAllAsync(Ada, TaskItemStatus.InProgress, "report");

        await _tasks.Received(1).ListAsync(Ada, TaskItemStatus.InProgress, "report", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_KeepsTheOrderTheRepositoryReturned()
    {
        GivenList(Ada, "Review the deck", "Write the report", "Archive last sprint");

        var mine = await _service.GetAllAsync(Ada, null, null);

        mine.Select(task => task.Title)
            .ShouldBe(["Review the deck", "Write the report", "Archive last sprint"]);
    }

    [Fact]
    public async Task GetAllAsync_WhenTheCallerOwnsNothing_ReturnsAnEmptyList()
    {
        GivenList(Ada);

        (await _service.GetAllAsync(Ada, null, null)).ShouldBeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwned_ReturnsTheTask()
    {
        var task = GivenTask(Ada, "Write the report", "Q1 batch", new DateOnly(2030, 1, 10));

        var found = await _service.GetByIdAsync(Ada, task.Id);

        found.Id.ShouldBe(task.Id);
        found.Title.ShouldBe("Write the report");
        found.Description.ShouldBe("Q1 batch");
        found.Status.ShouldBe(TaskItemStatus.Pending);
        found.DueDate.ShouldBe(new DateOnly(2030, 1, 10));
        found.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task GetByIdAsync_AsksTheRepositoryForTheTaskWithoutAnOwnerFilter()
    {
        var task = GivenTask(Ada);

        await _service.GetByIdAsync(Ada, task.Id);

        await _tasks.Received(1).GetByIdAsync(task.Id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetByIdAsync_WhenMissing_ThrowsNotFound()
    {
        var exception = await Should.ThrowAsync<NotFoundException>(
            () => _service.GetByIdAsync(Ada, Guid.NewGuid()));

        exception.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task GetByIdAsync_WhenOwnedByAnotherUser_ThrowsNotFound()
    {
        var task = GivenTask(Grace, "Quarterly forecast");

        var exception = await Should.ThrowAsync<NotFoundException>(() => _service.GetByIdAsync(Ada, task.Id));

        exception.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task GetByIdAsync_ForAForeignTask_FailsExactlyLikeForAMissingOne()
    {
        var foreign = GivenTask(Grace, "Quarterly forecast");

        var foreignFailure = await Should.ThrowAsync<NotFoundException>(() => _service.GetByIdAsync(Ada, foreign.Id));
        var missingFailure = await Should.ThrowAsync<NotFoundException>(() => _service.GetByIdAsync(Ada, Guid.NewGuid()));

        foreignFailure.Message.ShouldBe(missingFailure.Message);
    }

    [Fact]
    public async Task UpdateAsync_WhenOwned_AppliesChanges()
    {
        var task = GivenTask(Ada, "Write the report", "Q1 batch", new DateOnly(2030, 1, 10));

        var updated = await _service.UpdateAsync(
            Ada,
            task.Id,
            new UpdateTaskRequest("  Write the annual report  ", "  With the Q4 figures  ", TaskItemStatus.InProgress, new DateOnly(2030, 2, 1)));

        updated.Title.ShouldBe("Write the annual report");
        updated.Description.ShouldBe("With the Q4 figures");
        updated.Status.ShouldBe(TaskItemStatus.InProgress);
        updated.DueDate.ShouldBe(new DateOnly(2030, 2, 1));

        task.Title.ShouldBe("Write the annual report");
        task.Status.ShouldBe(TaskItemStatus.InProgress);

        await _tasks.Received(1).UpdateAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_ToDone_StampsTheCompletionTimeFromTheClock()
    {
        var task = GivenTask(Ada);
        _clock.UtcNow.Returns(Later);

        var updated = await _service.UpdateAsync(Ada, task.Id, Request(status: TaskItemStatus.Done));

        updated.Status.ShouldBe(TaskItemStatus.Done);
        updated.CompletedAt.ShouldBe(Later);
    }

    [Fact]
    public async Task UpdateAsync_OutOfDone_ClearsTheCompletionTime()
    {
        var task = GivenCompletedTask(Ada);
        _clock.UtcNow.Returns(Later);

        var updated = await _service.UpdateAsync(Ada, task.Id, Request(status: TaskItemStatus.Pending));

        updated.Status.ShouldBe(TaskItemStatus.Pending);
        updated.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenAlreadyDone_KeepsTheOriginalCompletionTime()
    {
        var task = GivenCompletedTask(Ada);
        _clock.UtcNow.Returns(Later);

        var updated = await _service.UpdateAsync(
            Ada,
            task.Id,
            Request(title: "Archive the last sprint", status: TaskItemStatus.Done));

        updated.Title.ShouldBe("Archive the last sprint");
        updated.CompletedAt.ShouldBe(Now);
    }

    [Fact]
    public async Task UpdateAsync_WithoutADueDate_ClearsTheDueDate()
    {
        var task = GivenTask(Ada, dueDate: new DateOnly(2030, 1, 10));

        var updated = await _service.UpdateAsync(Ada, task.Id, Request(dueDate: null));

        updated.DueDate.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateAsync_WithAPastDueDate_IsAllowed()
    {
        var task = GivenTask(Ada, dueDate: new DateOnly(2030, 1, 10));

        var updated = await _service.UpdateAsync(Ada, task.Id, Request(dueDate: new DateOnly(2020, 1, 1)));

        updated.DueDate.ShouldBe(new DateOnly(2020, 1, 1));
    }

    [Fact]
    public async Task UpdateAsync_WhenMissing_ThrowsNotFound()
    {
        var exception = await Should.ThrowAsync<NotFoundException>(
            () => _service.UpdateAsync(Ada, Guid.NewGuid(), Request()));

        exception.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task UpdateAsync_WhenOwnedByAnotherUser_ThrowsNotFound()
    {
        var task = GivenTask(Grace, "Quarterly forecast");

        var exception = await Should.ThrowAsync<NotFoundException>(
            () => _service.UpdateAsync(Ada, task.Id, Request(title: "Hijacked")));

        exception.Message.ShouldBe("Task not found.");
        task.Title.ShouldBe("Quarterly forecast");
        await _tasks.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task UpdateAsync_WithAnInvalidRequest_ThrowsValidationExceptionWithoutTouchingTheRepository(string title)
    {
        var task = GivenTask(Ada);

        await Should.ThrowAsync<ValidationException>(
            () => _service.UpdateAsync(Ada, task.Id, Request(title: title)));

        await _tasks.DidNotReceive().UpdateAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenOwned_RemovesTask()
    {
        var task = GivenTask(Ada, "Review the deck");

        await _service.DeleteAsync(Ada, task.Id);

        await _tasks.Received(1).DeleteAsync(task, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_WhenMissing_ThrowsNotFound()
    {
        var exception = await Should.ThrowAsync<NotFoundException>(
            () => _service.DeleteAsync(Ada, Guid.NewGuid()));

        exception.Message.ShouldBe("Task not found.");
    }

    [Fact]
    public async Task DeleteAsync_WhenOwnedByAnotherUser_ThrowsNotFound()
    {
        var task = GivenTask(Grace, "Quarterly forecast");

        var exception = await Should.ThrowAsync<NotFoundException>(() => _service.DeleteAsync(Ada, task.Id));

        exception.Message.ShouldBe("Task not found.");
        await _tasks.DidNotReceive().DeleteAsync(Arg.Any<TaskItem>(), Arg.Any<CancellationToken>());
    }

    private static UpdateTaskRequest Request(
        string title = "Write the report",
        string? description = null,
        TaskItemStatus status = TaskItemStatus.Pending,
        DateOnly? dueDate = null) =>
        new(title, description, status, dueDate);

    private TaskItem GivenTask(
        Guid ownerId,
        string title = "Write the report",
        string? description = null,
        DateOnly? dueDate = null)
    {
        var task = TaskItem.Create(title, description, dueDate, ownerId, Now);

        _tasks.GetByIdAsync(task.Id, Arg.Any<CancellationToken>()).Returns(task);

        return task;
    }

    private TaskItem GivenCompletedTask(Guid ownerId)
    {
        var task = GivenTask(ownerId, "Archive last sprint");

        task.ChangeStatus(TaskItemStatus.Done, Now);

        return task;
    }

    private void GivenList(Guid ownerId, params string[] titles)
    {
        var tasks = titles.Select(title => TaskItem.Create(title, null, null, ownerId, Now)).ToList();

        _tasks.ListAsync(ownerId, Arg.Any<TaskItemStatus?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(tasks);
    }
}
