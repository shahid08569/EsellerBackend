using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetHotSellingProducts;

public record GetHotSellingProductsQuery(
    int Limit = 20) : IRequest<Result<List<ProductListDto>>>;