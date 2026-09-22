using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.GetProductReviews;

public record GetProductReviewsQuery(
    Guid ProductId,
    int? Rating = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ReviewDto>>>;