using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminCatalog.SeedProductReviews;

public record SeedProductReviewsCommand(
    Guid ProductId,
    IReadOnlyList<SeedReviewItem> Reviews) : IRequest<Result<int>>;

public record SeedReviewItem(
    string ReviewerName,
    int Rating,
    string? Title,
    string? Comment);
