using FluentValidation;

namespace Eseller.Application.Features.ProductQuestions.UpdateAnswer;

public class UpdateAnswerCommandValidator : AbstractValidator<UpdateAnswerCommand>
{
    public UpdateAnswerCommandValidator()
    {
        RuleFor(x => x.QuestionId).NotEmpty();
        RuleFor(x => x.Answer).NotEmpty().MaximumLength(4000);
    }
}