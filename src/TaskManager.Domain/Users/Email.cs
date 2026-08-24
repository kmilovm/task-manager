using System.Text.RegularExpressions;
using TaskManager.Domain.Common;

namespace TaskManager.Domain.Users;

/// <summary>
/// A normalised email address. Comparison is case-insensitive because the address is
/// lowercased on the way in, which is what makes the unique index behave as BR-101 requires.
/// </summary>
public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? value)
    {
        DomainException.ThrowIfNullOrWhiteSpace(value, "Email is required.");

        var normalised = value!.Trim().ToLowerInvariant();

        if (normalised.Length > MaxLength)
        {
            throw new DomainException($"Email cannot exceed {MaxLength} characters.");
        }

        if (!AddressPattern().IsMatch(normalised))
        {
            throw new DomainException("Email is not a valid address.");
        }

        return new Email(normalised);
    }

    public override string ToString() => Value;

    // Deliberately permissive: the only way to prove an address is real is to send to it,
    // so this rejects obvious mistakes without pretending to implement RFC 5322.
    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}
