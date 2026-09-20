using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.GetProductVariantById;

public record GetProductVariantByIdQuery(
    Guid ProductId,
    Guid VariantId) : IRequest<Result<ProductVariantDto>>;