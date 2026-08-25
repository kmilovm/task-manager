using FluentValidation.TestHelper;
using TaskManager.Application.Users;

namespace TaskManager.Application.Tests.Users;

public class RegisterRequestValidatorTests
{
    private readonly RegisterRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAValidRequest_Succeeds()
    {
        _validator.TestValidate(Request()).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("short1")]
    [InlineData("password")]
    [InlineData("12345678")]
    public void Validate_WithWeakPassword_Fails(string password)
    {
        _validator.TestValidate(Request(password: password))
            .ShouldHaveValidationErrorFor(request => request.Password);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyDisplayName_Fails(string displayName)
    {
        _validator.TestValidate(Request(displayName: displayName))
            .ShouldHaveValidationErrorFor(request => request.DisplayName);
    }

    [Fact]
    public void Validate_WithTooLongDisplayName_Fails()
    {
        _validator.TestValidate(Request(displayName: new string('a', 101)))
            .ShouldHaveValidationErrorFor(request => request.DisplayName);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    public void Validate_WithInvalidEmail_Fails(string email)
    {
        _validator.TestValidate(Request(email: email))
            .ShouldHaveValidationErrorFor(request => request.Email);
    }

    [Fact]
    public void Validate_MeasuresTheDisplayNameAfterTrimmingIt()
    {
        var padded = $"  {new string('a', 100)}  ";

        _validator.TestValidate(Request(displayName: padded))
            .ShouldNotHaveValidationErrorFor(request => request.DisplayName);
    }

    private static RegisterRequest Request(
        string email = "grace@example.com",
        string displayName = "Grace Hopper",
        string password = "Passw0rd!") =>
        new(email, displayName, password);
}
