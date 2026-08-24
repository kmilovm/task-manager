using FluentValidation.TestHelper;
using TaskManager.Application.Users;

namespace TaskManager.Application.Tests.Users;

public class LoginRequestValidatorTests
{
    private readonly LoginRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAValidRequest_Succeeds()
    {
        _validator.TestValidate(new LoginRequest("ada@example.com", "Passw0rd!"))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithoutEmail_Fails()
    {
        _validator.TestValidate(new LoginRequest("", "Passw0rd!"))
            .ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Fact]
    public void Validate_WithoutPassword_Fails()
    {
        _validator.TestValidate(new LoginRequest("ada@example.com", ""))
            .ShouldHaveValidationErrorFor(request => request.Password);
    }

    [Fact]
    public void Validate_DoesNotApplyPasswordStrengthRulesOnLogin()
    {
        _validator.TestValidate(new LoginRequest("ada@example.com", "weak"))
            .ShouldNotHaveValidationErrorFor(request => request.Password);
    }
}
