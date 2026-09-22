using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.DeleteReview;

public record DeleteReviewCommand(Guid ReviewId) : IRequest<Result>;