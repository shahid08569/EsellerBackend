using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.UpdateAnswer;

public record UpdateAnswerCommand(
    Guid QuestionId,
    string Answer) : IRequest<Result>;