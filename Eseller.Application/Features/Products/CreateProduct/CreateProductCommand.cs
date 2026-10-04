using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.CreateProduct;

public record CreateProductCommand(
    Guid? ShopId,
    Guid CategoryId,
    Guid? BrandId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? SeoTitle,
    string? SeoDescription) : IRequest<Result<Guid>>;