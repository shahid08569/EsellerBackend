using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.ApproveReview;

public record ApproveReviewCommand(Guid ReviewId) : IRequest<Result>;