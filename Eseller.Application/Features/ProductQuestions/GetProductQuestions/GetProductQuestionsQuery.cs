using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.GetProductQuestions;

public record GetProductQuestionsQuery(
    Guid ProductId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductQuestionDto>>>;