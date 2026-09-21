using FluentValidation;

namespace Eseller.Application.Features.ProductQuestions.CreateQuestion;

public class CreateQuestionCommandValidator : AbstractValidator<CreateQuestionCommand>
{
    public CreateQuestionCommandValidator()
    {
        RuleFor(x => x.ProductId).NotEmpty();

        RuleFor(x => x.Question)
            .NotEmpty().WithMessage("Question is required.")
            .MaximumLength(2000).WithMessage("Question cannot exceed 2000 characters.");
    }
}