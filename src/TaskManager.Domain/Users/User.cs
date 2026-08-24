using TaskManager.Domain.Common;

namespace TaskManager.Domain.Users;

public sealed class User
{
    public const int MaxDisplayNameLength = 100;

    private User(Guid id, Email email, string displayName, string passwordHash, DateTimeOffset createdAt)
    {
        Id = id;
        Email = email;
        DisplayName = displayName;
        PasswordHash = passwordHash;
        CreatedAt = createdAt;
    }

    // Required by EF Core, which materialises entities without going through the factory.
    private User()
    {
        Email = null!;
        DisplayName = null!;
        PasswordHash = null!;
    }

    public Guid Id { get; private set; }

    public Email Email { get; private set; }

    public string DisplayName { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    /// <summary>
    /// Creates an account. The password is hashed before it reaches the domain: hashing is an
    /// infrastructure concern and the domain has no reason to know which algorithm was used.
    /// </summary>
    public static User Register(string? email, string? displayName, string? passwordHash, DateTimeOffset createdAt)
    {
        DomainException.ThrowIfNullOrWhiteSpace(displayName, "Display name is required.");
        DomainException.ThrowIfNullOrWhiteSpace(passwordHash, "Password hash is required.");

        var trimmedDisplayName = displayName!.Trim();

        if (trimmedDisplayName.Length > MaxDisplayNameLength)
        {
            throw new DomainException($"Display name cannot exceed {MaxDisplayNameLength} characters.");
        }

        return new User(Guid.NewGuid(), Email.Create(email), trimmedDisplayName, passwordHash!, createdAt);
    }
}
