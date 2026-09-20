using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.UpdateProduct;

public record UpdateProductCommand(
    Guid ProductId,
    Guid CategoryId,
    Guid? BrandId,
    string Name,
    string? Description,
    decimal BasePrice,
    string? SeoTitle,
    string? SeoDescription) : IRequest<Result>;