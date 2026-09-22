using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.GetPendingReviews;

public record GetPendingReviewsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ReviewDto>>>;