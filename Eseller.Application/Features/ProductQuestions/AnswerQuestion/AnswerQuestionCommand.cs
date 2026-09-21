using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.AnswerQuestion;

public record AnswerQuestionCommand(
    Guid QuestionId,
    string Answer) : IRequest<Result>;