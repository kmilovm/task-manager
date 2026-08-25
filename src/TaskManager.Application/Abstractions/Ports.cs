using TaskManager.Domain.Tasks;
using TaskManager.Domain.Users;

namespace TaskManager.Application.Abstractions;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public interface IPasswordHasher
{
    string Hash(string password);

    bool Verify(string password, string hash);
}

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);

public interface ITokenGenerator
{
    AccessToken Generate(User user);
}

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task<bool> ExistsWithEmailAsync(Email email, CancellationToken cancellationToken = default);

    Task AddAsync(User user, CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task<TaskItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<TaskItem>> ListAsync(
        Guid ownerId,
        TaskItemStatus? status,
        string? search,
        CancellationToken cancellationToken = default);

    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task UpdateAsync(TaskItem task, CancellationToken cancellationToken = default);

    Task DeleteAsync(TaskItem task, CancellationToken cancellationToken = default);
}
