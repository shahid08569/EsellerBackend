using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetOrderStatusHistory;

public record GetOrderStatusHistoryQuery(Guid OrderId)
    : IRequest<Result<IReadOnlyList<OrderStatusHistoryDto>>>;