using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Reviews.CreateReview;

public record CreateReviewCommand(
    Guid ProductId,
    Guid? OrderRequestItemId,
    int Rating,
    string? Title,
    string? Comment) : IRequest<Result<Guid>>;