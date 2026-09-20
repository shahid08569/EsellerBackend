using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetProductImages;

public record GetProductImagesQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyList<ProductImageDto>>>;