using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Search.SearchProducts;

public record SearchProductsQuery(
    string Query,
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? ShopId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    decimal? MinRating = null,
    bool? IsFeatured = null,
    string SortBy = "relevance",
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<SearchResultDto>>>;