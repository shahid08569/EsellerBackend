using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetCatalogProducts;

public record GetCatalogProductsQuery(
    Guid? CategoryId = null,
    Guid? BrandId = null,
    string? Search = null,
    string SortBy = "newest",
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;
