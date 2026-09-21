using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.RejectReview;

public record RejectReviewCommand(Guid ReviewId) : IRequest<Result>;