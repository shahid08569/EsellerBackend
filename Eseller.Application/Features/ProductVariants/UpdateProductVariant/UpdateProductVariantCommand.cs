using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.UpdateProductVariant;

public record UpdateProductVariantCommand(
    Guid ProductId,
    Guid VariantId,
    string Sku,
    decimal Price,
    int StockQty,
    int LowStockThreshold,
    bool IsActive,
    List<UpdateVariantAttribute> Attributes) : IRequest<Result>;

public record UpdateVariantAttribute(
    string AttributeName,
    string AttributeValue);