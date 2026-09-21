using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.UpdateReview;

public record UpdateReviewCommand(
    Guid ReviewId,
    int Rating,
    string? Title,
    string? Comment) : IRequest<Result>;