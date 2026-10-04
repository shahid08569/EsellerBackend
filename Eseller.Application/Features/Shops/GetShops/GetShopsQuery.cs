using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.GetShops;

public record GetShopsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    Guid? ShopCategoryId = null) : IRequest<Result<PagedList<ShopListDto>>>;