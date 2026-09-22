using FluentValidation;

namespace Eseller.Application.Features.ProductQuestions.AnswerQuestion;

public class AnswerQuestionCommandValidator : AbstractValidator<AnswerQuestionCommand>
{
    public AnswerQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();

        RuleFor(x => x.Answer)
            .NotEmpty().WithMessage("Answer is required.")
            .MaximumLength(4000).WithMessage("Answer cannot exceed 4000 characters.");
    }
}