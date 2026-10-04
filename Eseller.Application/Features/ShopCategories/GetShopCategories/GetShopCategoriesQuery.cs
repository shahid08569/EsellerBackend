using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ShopCategories.GetShopCategories;

public record GetShopCategoriesQuery(bool OnlyActive = false)
    : IRequest<Result<IReadOnlyList<ShopCategoryDto>>>;
