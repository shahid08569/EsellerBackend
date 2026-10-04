using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetLowStockProducts;

public record GetLowStockProductsQuery(
    int Limit = 20) : IRequest<Result<List<ProductListDto>>>;