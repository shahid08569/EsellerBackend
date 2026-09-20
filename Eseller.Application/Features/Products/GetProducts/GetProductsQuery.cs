using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.GetProducts;

public record GetProductsQuery(
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? ShopId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    string? Search = null,
    bool? IsFeatured = null,
    string SortBy = "newest",
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<ProductListDto>>>;