using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductQuestions.GetPendingQuestions;

public record GetPendingQuestionsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductQuestionDto>>>;