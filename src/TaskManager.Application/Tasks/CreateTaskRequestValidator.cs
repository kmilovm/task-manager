using FluentValidation;
using TaskManager.Application.Common;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumTrimmedLength(TaskItem.MaxTitleLength);

        RuleFor(request => request.Description)
            .MaximumTrimmedLength(TaskItem.MaxDescriptionLength);
    }
}
