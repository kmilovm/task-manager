using FluentValidation.TestHelper;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tests.Tasks;

public class CreateTaskRequestValidatorTests
{
    private readonly CreateTaskRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAValidRequest_Succeeds()
    {
        _validator.TestValidate(Request(dueDate: new DateOnly(2030, 3, 31)))
            .ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithTitleOnly_Succeeds()
    {
        _validator.TestValidate(Request(description: null)).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithEmptyTitle_Fails(string title)
    {
        _validator.TestValidate(Request(title: title))
            .ShouldHaveValidationErrorFor(request => request.Title);
    }

    [Fact]
    public void Validate_WithTooLongTitle_Fails()
    {
        _validator.TestValidate(Request(title: new string('a', TaskItem.MaxTitleLength + 1)))
            .ShouldHaveValidationErrorFor(request => request.Title);
    }

    [Fact]
    public void Validate_WithATitleOfExactlyTheMaximumLength_Succeeds()
    {
        _validator.TestValidate(Request(title: new string('a', TaskItem.MaxTitleLength)))
            .ShouldNotHaveValidationErrorFor(request => request.Title);
    }

    [Fact]
    public void Validate_WithTooLongDescription_Fails()
    {
        _validator.TestValidate(Request(description: new string('a', TaskItem.MaxDescriptionLength + 1)))
            .ShouldHaveValidationErrorFor(request => request.Description);
    }

    [Fact]
    public void Validate_WithADescriptionOfExactlyTheMaximumLength_Succeeds()
    {
        _validator.TestValidate(Request(description: new string('a', TaskItem.MaxDescriptionLength)))
            .ShouldNotHaveValidationErrorFor(request => request.Description);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_WithoutADescription_Succeeds(string? description)
    {
        _validator.TestValidate(Request(description: description))
            .ShouldNotHaveValidationErrorFor(request => request.Description);
    }

    [Fact]
    public void Validate_WithAPastDueDate_Succeeds()
    {
        _validator.TestValidate(Request(dueDate: new DateOnly(2020, 1, 1)))
            .ShouldNotHaveValidationErrorFor(request => request.DueDate);
    }

    [Fact]
    public void Validate_MeasuresTheTitleAfterTrimmingIt()
    {
        var padded = $"  {new string('a', 200)}  ";

        _validator.TestValidate(Request(title: padded)).ShouldNotHaveValidationErrorFor(request => request.Title);
    }

    [Fact]
    public void Validate_MeasuresTheDescriptionAfterTrimmingIt()
    {
        var padded = $"  {new string('a', 2000)}  ";

        _validator.TestValidate(Request(description: padded))
            .ShouldNotHaveValidationErrorFor(request => request.Description);
    }

    private static CreateTaskRequest Request(
        string title = "Prepare invoices",
        string? description = "Q1 batch",
        DateOnly? dueDate = null) =>
        new(title, description, dueDate);
}
