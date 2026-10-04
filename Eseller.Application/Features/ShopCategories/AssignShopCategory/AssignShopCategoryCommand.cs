using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ShopCategories.AssignShopCategory;

public record AssignShopCategoryCommand(
    Guid ShopId,
    Guid? ShopCategoryId,
    string? CustomBadgeText = null) : IRequest<Result>;
