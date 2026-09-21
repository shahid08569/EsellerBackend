using FluentValidation;

namespace Eseller.Application.Features.ProductQuestions.UpdateQuestion;

public class UpdateQuestionCommandValidator : AbstractValidator<UpdateQuestionCommand>
{
    public UpdateQuestionCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Question).NotEmpty().MaximumLength(2000);
    }
}