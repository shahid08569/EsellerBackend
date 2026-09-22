using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.DeleteQuestion;

public record DeleteQuestionCommand(Guid QuestionId) : IRequest<Result>;