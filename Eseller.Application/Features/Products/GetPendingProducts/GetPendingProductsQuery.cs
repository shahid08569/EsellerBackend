using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetPendingProducts;

public record GetPendingProductsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;