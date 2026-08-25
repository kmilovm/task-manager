using FluentValidation;
using TaskManager.Domain.Tasks;

namespace TaskManager.Application.Tasks;

public sealed class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(request => request.Title)
            .NotEmpty()
            .MaximumLength(TaskItem.MaxTitleLength);

        RuleFor(request => request.Description)
            .MaximumLength(TaskItem.MaxDescriptionLength);

        RuleFor(request => request.Status).IsInEnum();
    }
}
