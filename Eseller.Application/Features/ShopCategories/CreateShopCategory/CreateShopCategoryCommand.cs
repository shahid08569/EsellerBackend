using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ShopCategories.CreateShopCategory;

public record CreateShopCategoryCommand(
    string Name,
    string? Slug,
    string? Description,
    string? BadgeText,
    string? BadgeColor,
    string? IconUrl,
    int DisplayOrder = 0,
    decimal PriceUsd = 0,
    int MaxProductListings = 200) : IRequest<Result<Guid>>;
