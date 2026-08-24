namespace TaskManager.Domain.Common;

/// <summary>
/// Raised when an operation would leave an entity in a state its invariants forbid.
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message)
        : base(message)
    {
    }

    public static void ThrowIfNullOrWhiteSpace(string? value, string message)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException(message);
        }
    }
}
