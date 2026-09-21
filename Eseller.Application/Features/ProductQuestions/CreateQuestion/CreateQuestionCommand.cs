using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.CreateQuestion;

public record CreateQuestionCommand(
    Guid ProductId,
    string Question) : IRequest<Result<Guid>>;