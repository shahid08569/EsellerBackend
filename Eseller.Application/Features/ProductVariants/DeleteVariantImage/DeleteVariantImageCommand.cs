using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.DeleteVariantImage;

public record DeleteVariantImageCommand(
    Guid ProductId,
    Guid VariantId) : IRequest<Result>;