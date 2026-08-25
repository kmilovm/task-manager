using TaskManager.Domain.Common;

namespace TaskManager.Domain.Tasks;

public sealed class TaskItem
{
    public const int MaxTitleLength = 200;

    public const int MaxDescriptionLength = 2000;

    private TaskItem(
        Guid id,
        string title,
        string? description,
        DateOnly? dueDate,
        Guid ownerId,
        DateTimeOffset createdAt)
    {
        Id = id;
        Title = title;
        Description = description;
        Status = TaskItemStatus.Pending;
        DueDate = dueDate;
        OwnerId = ownerId;
        CreatedAt = createdAt;
    }

    private TaskItem() => Title = null!;

    public Guid Id { get; private set; }

    public string Title { get; private set; }

    public string? Description { get; private set; }

    public TaskItemStatus Status { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public Guid OwnerId { get; private set; }

    public static TaskItem Create(
        string? title,
        string? description,
        DateOnly? dueDate,
        Guid ownerId,
        DateTimeOffset createdAt)
    {
        if (ownerId == Guid.Empty)
        {
            throw new DomainException("Owner is required.");
        }

        var normalisedTitle = NormaliseTitle(title);
        var normalisedDescription = NormaliseDescription(description);

        if (dueDate is { } due && due < DateOnly.FromDateTime(createdAt.UtcDateTime))
        {
            throw new DomainException("Due date cannot be in the past.");
        }

        return new TaskItem(Guid.NewGuid(), normalisedTitle, normalisedDescription, dueDate, ownerId, createdAt);
    }

    public void Update(string? title, string? description, DateOnly? dueDate)
    {
        var normalisedTitle = NormaliseTitle(title);
        var normalisedDescription = NormaliseDescription(description);

        Title = normalisedTitle;
        Description = normalisedDescription;
        DueDate = dueDate;
    }

    public void ChangeStatus(TaskItemStatus status, DateTimeOffset occurredAt)
    {
        if (!Enum.IsDefined(status))
        {
            throw new DomainException("Status is not a known task status.");
        }

        if (status == Status)
        {
            return;
        }

        Status = status;
        CompletedAt = status == TaskItemStatus.Done ? occurredAt : null;
    }

    private static string NormaliseTitle(string? title)
    {
        DomainException.ThrowIfNullOrWhiteSpace(title, "Title is required.");

        var trimmed = title!.Trim();

        return trimmed.Length > MaxTitleLength
            ? throw new DomainException($"Title cannot exceed {MaxTitleLength} characters.")
            : trimmed;
    }

    private static string? NormaliseDescription(string? description)
    {
        if (string.IsNullOrWhiteSpace(description))
        {
            return null;
        }

        var trimmed = description.Trim();

        return trimmed.Length > MaxDescriptionLength
            ? throw new DomainException($"Description cannot exceed {MaxDescriptionLength} characters.")
            : trimmed;
    }
}
