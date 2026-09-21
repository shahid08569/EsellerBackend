using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.RejectQuestion;

public record RejectQuestionCommand(Guid QuestionId) : IRequest<Result>;