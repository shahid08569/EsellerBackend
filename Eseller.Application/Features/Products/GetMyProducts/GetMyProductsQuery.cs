using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetMyProducts;

public record GetMyProductsQuery(
    Guid ShopId,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;