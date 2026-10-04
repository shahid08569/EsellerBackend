using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetNewArrivals;

public record GetNewArrivalsQuery(
    int Limit = 20) : IRequest<Result<List<ProductListDto>>>;