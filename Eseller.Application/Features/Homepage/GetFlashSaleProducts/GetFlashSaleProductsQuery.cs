using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetFlashSaleProducts;

public record GetFlashSaleProductsQuery(
    int Limit = 20) : IRequest<Result<List<ProductListDto>>>;