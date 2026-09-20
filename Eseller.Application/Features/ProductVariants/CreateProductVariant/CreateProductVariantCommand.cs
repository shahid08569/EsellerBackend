using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.CreateProductVariant;

public record CreateProductVariantCommand(
    Guid ProductId,
    string Sku,
    decimal Price,
    int StockQty,
    int LowStockThreshold,
    List<CreateVariantAttribute> Attributes) : IRequest<Result<Guid>>;

public record CreateVariantAttribute(
    string AttributeName,
    string AttributeValue);