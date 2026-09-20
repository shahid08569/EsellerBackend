using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.DeleteProductVariant;

public record DeleteProductVariantCommand(
    Guid ProductId,
    Guid VariantId) : IRequest<Result>;