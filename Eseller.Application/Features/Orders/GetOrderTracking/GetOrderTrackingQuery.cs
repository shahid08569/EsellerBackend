using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetOrderTracking;

public record GetOrderTrackingQuery(Guid OrderId) : IRequest<Result<OrderTrackingDto>>;