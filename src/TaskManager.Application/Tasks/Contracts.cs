using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public sealed record CreateTaskRequest(string Title, string? Description, DateOnly? DueDate);

public sealed record UpdateTaskRequest(string Title, string? Description, TaskItemStatus Status, DateOnly? DueDate);

public sealed record TaskDto(
    Guid Id,
    string Title,
    string? Description,
    TaskItemStatus Status,
    DateOnly? DueDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset? CompletedAt);
