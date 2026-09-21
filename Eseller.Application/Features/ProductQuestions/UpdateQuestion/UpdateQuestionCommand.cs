using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.UpdateQuestion;

public record UpdateQuestionCommand(
    Guid QuestionId,
    string Question) : IRequest<Result>;