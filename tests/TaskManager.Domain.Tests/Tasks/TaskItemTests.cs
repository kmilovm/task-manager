using Shouldly;
using TaskManager.Domain.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Domain.Tests.Tasks;

public class TaskItemTests
{
    private static readonly DateTimeOffset Now = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset Later = Now.AddHours(3);

    private static readonly DateOnly Today = new(2026, 1, 15);

    private static readonly Guid Owner = new("11111111-1111-1111-1111-111111111111");

    [Fact]
    public void Create_WithTitleOnly_StartsAsPendingWithoutDueDate()
    {
        var task = Create(title: "Book the meeting room");

        task.Title.ShouldBe("Book the meeting room");
        task.Status.ShouldBe(TaskItemStatus.Pending);
        task.DueDate.ShouldBeNull();
        task.Description.ShouldBeNull();
        task.CompletedAt.ShouldBeNull();
        task.OwnerId.ShouldBe(Owner);
    }

    [Fact]
    public void Create_TrimsTitle()
    {
        Create(title: "  Call the supplier  ").Title.ShouldBe("Call the supplier");
    }

    [Fact]
    public void Create_AssignsAnIdentifierAndTheGivenCreationTime()
    {
        var task = Create();

        task.Id.ShouldNotBe(Guid.Empty);
        task.CreatedAt.ShouldBe(Now);
    }

    [Fact]
    public void Create_GivesEveryTaskADistinctIdentifier()
    {
        Create().Id.ShouldNotBe(Create().Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutATitle_Throws(string? title)
    {
        Should.Throw<DomainException>(() => Create(title: title));
    }

    [Fact]
    public void Create_WithATitleLongerThan200Characters_Throws()
    {
        Should.Throw<DomainException>(() => Create(title: new string('a', 201)));
    }

    [Fact]
    public void Create_WithATitleOfExactly200Characters_Succeeds()
    {
        Create(title: new string('a', 200)).Title.Length.ShouldBe(200);
    }

    [Fact]
    public void Create_MeasuresTheTitleAfterTrimmingIt()
    {
        Create(title: "  " + new string('a', 200) + "  ").Title.Length.ShouldBe(200);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithoutADescription_StoresNoDescription(string? description)
    {
        Create(description: description).Description.ShouldBeNull();
    }

    [Fact]
    public void Create_TrimsDescription()
    {
        Create(description: "  Q1 batch  ").Description.ShouldBe("Q1 batch");
    }

    [Fact]
    public void Create_WithADescriptionLongerThan2000Characters_Throws()
    {
        Should.Throw<DomainException>(() => Create(description: new string('a', 2001)));
    }

    [Fact]
    public void Create_WithADescriptionOfExactly2000Characters_Succeeds()
    {
        Create(description: new string('a', 2000)).Description!.Length.ShouldBe(2000);
    }

    [Fact]
    public void Create_WithoutAnOwner_Throws()
    {
        Should.Throw<DomainException>(() => Create(ownerId: Guid.Empty));
    }

    [Fact]
    public void Create_WithAFutureDueDate_KeepsIt()
    {
        Create(dueDate: new DateOnly(2030, 3, 31)).DueDate.ShouldBe(new DateOnly(2030, 3, 31));
    }

    [Fact]
    public void Create_WithTodayAsTheDueDate_Succeeds()
    {
        Create(dueDate: Today).DueDate.ShouldBe(Today);
    }

    [Fact]
    public void Create_WithPastDueDate_Throws()
    {
        var exception = Should.Throw<DomainException>(() => Create(dueDate: new DateOnly(2020, 1, 1)));

        exception.Message.ShouldBe("Due date cannot be in the past.");
    }

    [Fact]
    public void Create_WithYesterdayAsTheDueDate_Throws()
    {
        Should.Throw<DomainException>(() => Create(dueDate: Today.AddDays(-1)));
    }

    [Fact]
    public void Create_ComparesTheDueDateAgainstTodayInUtcRatherThanLocalTime()
    {
        var justAfterLocalMidnight = new DateTimeOffset(2026, 1, 16, 0, 30, 0, TimeSpan.FromHours(2));

        var task = Create(dueDate: new DateOnly(2026, 1, 15), createdAt: justAfterLocalMidnight);

        task.DueDate.ShouldBe(new DateOnly(2026, 1, 15));
    }

    [Fact]
    public void ChangeStatus_ToDone_SetsCompletedAt()
    {
        var task = Create();

        task.ChangeStatus(TaskItemStatus.Done, Later);

        task.Status.ShouldBe(TaskItemStatus.Done);
        task.CompletedAt.ShouldBe(Later);
    }

    [Theory]
    [InlineData(TaskItemStatus.Pending)]
    [InlineData(TaskItemStatus.InProgress)]
    public void ChangeStatus_FromDone_ClearsCompletedAt(TaskItemStatus status)
    {
        var task = Completed();

        task.ChangeStatus(status, Later.AddHours(1));

        task.Status.ShouldBe(status);
        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void ChangeStatus_WhenAlreadyDone_KeepsCompletedAt()
    {
        var task = Completed();

        task.ChangeStatus(TaskItemStatus.Done, Later.AddDays(2));

        task.CompletedAt.ShouldBe(Later);
    }

    [Fact]
    public void ChangeStatus_ToDoneAfterReopening_StampsTheNewCompletionTime()
    {
        var task = Completed();
        var finishedAgain = Later.AddDays(4);

        task.ChangeStatus(TaskItemStatus.InProgress, Later.AddDays(3));
        task.ChangeStatus(TaskItemStatus.Done, finishedAgain);

        task.CompletedAt.ShouldBe(finishedAgain);
    }

    [Fact]
    public void ChangeStatus_BetweenUnfinishedStatuses_LeavesCompletedAtEmpty()
    {
        var task = Create();

        task.ChangeStatus(TaskItemStatus.InProgress, Later);

        task.Status.ShouldBe(TaskItemStatus.InProgress);
        task.CompletedAt.ShouldBeNull();
    }

    [Fact]
    public void ChangeStatus_WithAStatusOutsideTheEnumeration_Throws()
    {
        var task = Create();

        Should.Throw<DomainException>(() => task.ChangeStatus((TaskItemStatus)99, Later));
    }

    [Fact]
    public void Update_AppliesTitleDescriptionAndDueDate()
    {
        var task = Create();

        task.Update("Write the annual report", "With the Q4 figures", new DateOnly(2030, 6, 1));

        task.Title.ShouldBe("Write the annual report");
        task.Description.ShouldBe("With the Q4 figures");
        task.DueDate.ShouldBe(new DateOnly(2030, 6, 1));
    }

    [Fact]
    public void Update_TrimsTitleAndDescription()
    {
        var task = Create();

        task.Update("  Call the supplier  ", "  Q1 batch  ", null);

        task.Title.ShouldBe("Call the supplier");
        task.Description.ShouldBe("Q1 batch");
    }

    [Fact]
    public void Update_WithNullDueDate_ClearsDueDate()
    {
        var task = Create(dueDate: new DateOnly(2030, 1, 10));

        task.Update(task.Title, task.Description, null);

        task.DueDate.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithoutADescription_ClearsDescription(string? description)
    {
        var task = Create(description: "Q1 batch");

        task.Update(task.Title, description, null);

        task.Description.ShouldBeNull();
    }

    [Fact]
    public void Update_WithAPastDueDate_IsAllowed()
    {
        var task = Create(dueDate: new DateOnly(2030, 1, 10));

        task.Update(task.Title, task.Description, new DateOnly(2020, 1, 1));

        task.DueDate.ShouldBe(new DateOnly(2020, 1, 1));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Update_WithoutATitle_Throws(string? title)
    {
        var task = Create();

        Should.Throw<DomainException>(() => task.Update(title, null, null));
    }

    [Fact]
    public void Update_WithATitleLongerThan200Characters_Throws()
    {
        var task = Create();

        Should.Throw<DomainException>(() => task.Update(new string('a', 201), null, null));
    }

    [Fact]
    public void Update_WithADescriptionLongerThan2000Characters_Throws()
    {
        var task = Create();

        Should.Throw<DomainException>(() => task.Update(task.Title, new string('a', 2001), null));
    }

    [Fact]
    public void Update_LeavesTheStatusAndTheCompletionTimeAlone()
    {
        var task = Completed();

        task.Update("Archive the last sprint", null, null);

        task.Status.ShouldBe(TaskItemStatus.Done);
        task.CompletedAt.ShouldBe(Later);
    }

    [Fact]
    public void Update_WhenItFails_LeavesTheTaskUnchanged()
    {
        var task = Create(title: "Write the report", description: "Q1 batch", dueDate: new DateOnly(2030, 1, 10));

        Should.Throw<DomainException>(() => task.Update(new string('a', 201), "Replaced", null));

        task.Title.ShouldBe("Write the report");
        task.Description.ShouldBe("Q1 batch");
        task.DueDate.ShouldBe(new DateOnly(2030, 1, 10));
    }

    private static TaskItem Completed()
    {
        var task = Create();

        task.ChangeStatus(TaskItemStatus.Done, Later);

        return task;
    }

    private static TaskItem Create(
        string? title = "Write the report",
        string? description = null,
        DateOnly? dueDate = null,
        Guid? ownerId = null,
        DateTimeOffset? createdAt = null) =>
        TaskItem.Create(title, description, dueDate, ownerId ?? Owner, createdAt ?? Now);
}
