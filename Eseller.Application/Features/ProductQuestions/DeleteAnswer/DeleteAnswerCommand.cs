using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.DeleteAnswer;

public record DeleteAnswerCommand(Guid QuestionId) : IRequest<Result>;