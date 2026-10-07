using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetPendingProducts;

/// <param name="Kind">
/// null/all = every pending seller listing;
/// "global" = warehouse add-to-store requests (SourceProductId set);
/// "own" = seller-created products (SourceProductId null).
/// </param>
public record GetPendingProductsQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Kind = null) : IRequest<Result<PagedList<ProductListDto>>>;