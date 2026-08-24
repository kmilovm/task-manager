using System.Text.RegularExpressions;
using TaskManager.Domain.Common;

namespace TaskManager.Domain.Users;

public sealed partial record Email
{
    public const int MaxLength = 254;

    private Email(string value) => Value = value;

    public string Value { get; }

    public static Email Create(string? value)
    {
        DomainException.ThrowIfNullOrWhiteSpace(value, "Email is required.");

        var normalised = Normalise(value!);

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

    public static bool IsValid(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && Normalise(value) is { Length: <= MaxLength } normalised
        && AddressPattern().IsMatch(normalised);

    public override string ToString() => Value;

    private static string Normalise(string value) => value.Trim().ToLowerInvariant();

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", RegexOptions.CultureInvariant)]
    private static partial Regex AddressPattern();
}
