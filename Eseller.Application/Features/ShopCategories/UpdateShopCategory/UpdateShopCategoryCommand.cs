using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ShopCategories.UpdateShopCategory;

public record UpdateShopCategoryCommand(
    Guid Id,
    string Name,
    string? Slug,
    string? Description,
    string? BadgeText,
    string? BadgeColor,
    string? IconUrl,
    int DisplayOrder,
    bool IsActive,
    decimal PriceUsd = 0,
    int MaxProductListings = 200) : IRequest<Result>;
