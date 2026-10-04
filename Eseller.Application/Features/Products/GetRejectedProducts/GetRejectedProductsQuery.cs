using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetRejectedProducts;

public record GetRejectedProductsQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;
