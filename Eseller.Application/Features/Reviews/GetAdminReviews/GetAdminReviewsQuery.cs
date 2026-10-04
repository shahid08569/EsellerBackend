using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.GetAdminReviews;

public record GetAdminReviewsQuery(
    string Status = "pending",
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ReviewDto>>>;
