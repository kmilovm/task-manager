using FluentValidation.TestHelper;
using TaskManager.Application.Tasks;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tests.Tasks;

public class UpdateTaskRequestValidatorTests
{
    private readonly UpdateTaskRequestValidator _validator = new();

    [Fact]
    public void Validate_WithAValidRequest_Succeeds()
    {
        _validator.TestValidate(Request(dueDate: new DateOnly(2030, 3, 31)))
            .ShouldNotHaveAnyValidationErrors();
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
    public void Validate_WithTooLongDescription_Fails()
    {
        _validator.TestValidate(Request(description: new string('a', TaskItem.MaxDescriptionLength + 1)))
            .ShouldHaveValidationErrorFor(request => request.Description);
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
    public void Validate_WithoutADueDate_Succeeds()
    {
        _validator.TestValidate(Request(dueDate: null))
            .ShouldNotHaveValidationErrorFor(request => request.DueDate);
    }

    [Fact]
    public void Validate_WithAPastDueDate_Succeeds()
    {
        _validator.TestValidate(Request(dueDate: new DateOnly(2020, 1, 1)))
            .ShouldNotHaveValidationErrorFor(request => request.DueDate);
    }

    [Theory]
    [InlineData(TaskItemStatus.Pending)]
    [InlineData(TaskItemStatus.InProgress)]
    [InlineData(TaskItemStatus.Done)]
    public void Validate_WithAKnownStatus_Succeeds(TaskItemStatus status)
    {
        _validator.TestValidate(Request(status: status))
            .ShouldNotHaveValidationErrorFor(request => request.Status);
    }

    [Fact]
    public void Validate_WithAStatusOutsideTheEnumeration_Fails()
    {
        _validator.TestValidate(Request(status: (TaskItemStatus)99))
            .ShouldHaveValidationErrorFor(request => request.Status);
    }

    private static UpdateTaskRequest Request(
        string title = "Write the annual report",
        string? description = "With the Q4 figures",
        TaskItemStatus status = TaskItemStatus.InProgress,
        DateOnly? dueDate = null) =>
        new(title, description, status, dueDate);
}
