using FluentValidation;
using FluentValidation.Results;
using TaskManager.Application.Abstractions;
using TaskManager.Application.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public interface ITaskService
{
    Task<IReadOnlyList<TaskDto>> GetAllAsync(
        Guid ownerId,
        TaskItemStatus? status,
        string? search,
        CancellationToken cancellationToken = default);

    Task<TaskDto> GetByIdAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);

    Task<TaskDto> CreateAsync(Guid ownerId, CreateTaskRequest request, CancellationToken cancellationToken = default);

    Task<TaskDto> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default);
}

public sealed class TaskService : ITaskService
{
    private readonly ITaskRepository _tasks;
    private readonly IClock _clock;
    private readonly IValidator<CreateTaskRequest> _createValidator;
    private readonly IValidator<UpdateTaskRequest> _updateValidator;

    public TaskService(
        ITaskRepository tasks,
        IClock clock,
        IValidator<CreateTaskRequest> createValidator,
        IValidator<UpdateTaskRequest> updateValidator)
    {
        _tasks = tasks;
        _clock = clock;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<IReadOnlyList<TaskDto>> GetAllAsync(
        Guid ownerId,
        TaskItemStatus? status,
        string? search,
        CancellationToken cancellationToken = default)
    {
        // A value outside the enumeration binds happily and would silently match nothing, so the
        // caller would read an empty list as "you have no such tasks" instead of "no such status".
        if (status is { } wanted && !Enum.IsDefined(wanted))
        {
            throw new ValidationException([
                new ValidationFailure(
                    "Status",
                    $"'Status' has a range of values which does not include '{(int)wanted}'.")
            ]);
        }

        var tasks = await _tasks.ListAsync(ownerId, status, search, cancellationToken);

        return tasks.Select(ToDto).ToList();
    }

    public async Task<TaskDto> GetByIdAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default) =>
        ToDto(await RequireOwnedAsync(id, ownerId, cancellationToken));

    public async Task<TaskDto> CreateAsync(
        Guid ownerId,
        CreateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await _createValidator.ValidateAndThrowAsync(request, cancellationToken);

        var task = TaskItem.Create(request.Title, request.Description, request.DueDate, ownerId, _clock.UtcNow);

        await _tasks.AddAsync(task, cancellationToken);

        return ToDto(task);
    }

    public async Task<TaskDto> UpdateAsync(
        Guid ownerId,
        Guid id,
        UpdateTaskRequest request,
        CancellationToken cancellationToken = default)
    {
        await _updateValidator.ValidateAndThrowAsync(request, cancellationToken);

        var task = await RequireOwnedAsync(id, ownerId, cancellationToken);

        task.Update(request.Title, request.Description, request.DueDate);
        task.ChangeStatus(request.Status, _clock.UtcNow);

        await _tasks.UpdateAsync(task, cancellationToken);

        return ToDto(task);
    }

    public async Task DeleteAsync(Guid ownerId, Guid id, CancellationToken cancellationToken = default)
    {
        var task = await RequireOwnedAsync(id, ownerId, cancellationToken);

        await _tasks.DeleteAsync(task, cancellationToken);
    }

    private static TaskDto ToDto(TaskItem task) =>
        new(task.Id, task.Title, task.Description, task.Status, task.DueDate, task.CreatedAt, task.CompletedAt);

    private async Task<TaskItem> RequireOwnedAsync(Guid id, Guid ownerId, CancellationToken cancellationToken)
    {
        var task = await _tasks.GetByIdAsync(id, cancellationToken);

        return task is null || task.OwnerId != ownerId
            ? throw new NotFoundException("Task not found.")
            : task;
    }
}
