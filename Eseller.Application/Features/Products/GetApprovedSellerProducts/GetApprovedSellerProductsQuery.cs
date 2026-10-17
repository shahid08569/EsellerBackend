using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetApprovedSellerProducts;

public record GetApprovedSellerProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Kind = null,
    Guid? ShopId = null,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    string? Search = null) : IRequest<Result<PagedList<ProductListDto>>>;
