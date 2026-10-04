using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetApprovedSellerProducts;

public record GetApprovedSellerProductsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;
