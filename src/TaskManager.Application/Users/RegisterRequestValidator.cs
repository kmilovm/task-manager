using FluentValidation;
using TaskManager.Application.Common;
using TaskManager.Domain.Users;

namespace TaskManager.Application.Users;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public const int MinimumPasswordLength = 8;

    public RegisterRequestValidator()
    {
        RuleFor(request => request.Email)
            .Must(Email.IsValid)
            .WithMessage("Email is not a valid address.");

        RuleFor(request => request.DisplayName)
            .NotEmpty()
            .MaximumTrimmedLength(User.MaxDisplayNameLength);

        RuleFor(request => request.Password)
            .NotEmpty()
            .MinimumLength(MinimumPasswordLength)
            .Matches("[A-Za-z]").WithMessage("Password must contain a letter.")
            .Matches("[0-9]").WithMessage("Password must contain a digit.");
    }
}
