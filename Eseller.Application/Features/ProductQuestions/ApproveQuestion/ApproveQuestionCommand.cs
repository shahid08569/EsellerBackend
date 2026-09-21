using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.ApproveQuestion;

public record ApproveQuestionCommand(Guid QuestionId) : IRequest<Result>;