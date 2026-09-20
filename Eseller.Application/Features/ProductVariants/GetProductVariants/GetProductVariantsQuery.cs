using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.GetProductVariants;

public record GetProductVariantsQuery(Guid ProductId)
    : IRequest<Result<IReadOnlyList<ProductVariantListDto>>>;