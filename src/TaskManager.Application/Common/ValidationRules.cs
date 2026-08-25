using FluentValidation;

namespace TaskManager.Application.Common;

public static class ValidationRules
{
    /// <summary>
    /// Measures what the domain measures. The entities trim before they check a length, so a
    /// boundary rule reading the raw string would reject a value the domain would have accepted.
    /// </summary>
    public static IRuleBuilderOptions<T, string?> MaximumTrimmedLength<T>(
        this IRuleBuilder<T, string?> rule,
        int maximumLength) =>
        rule.Must(value => value is null || value.Trim().Length <= maximumLength)
            .WithMessage($"'{{PropertyName}}' cannot exceed {maximumLength} characters.");
}
