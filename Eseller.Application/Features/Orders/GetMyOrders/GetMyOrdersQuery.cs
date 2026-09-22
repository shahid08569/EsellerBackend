using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetMyOrders;

public record GetMyOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<OrderRequestListDto>>>;