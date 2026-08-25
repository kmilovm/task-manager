using FluentValidation;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public sealed class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(TaskItem.MaxTitleLength);

        RuleFor(request => request.Description)
            .MaximumLength(TaskItem.MaxDescriptionLength);
    }
}
