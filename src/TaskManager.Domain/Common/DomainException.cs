namespace TaskManager.Domain.Common;

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
