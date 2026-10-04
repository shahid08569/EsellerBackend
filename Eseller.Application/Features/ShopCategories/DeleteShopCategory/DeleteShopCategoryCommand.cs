using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ShopCategories.DeleteShopCategory;

public record DeleteShopCategoryCommand(Guid Id) : IRequest<Result>;
