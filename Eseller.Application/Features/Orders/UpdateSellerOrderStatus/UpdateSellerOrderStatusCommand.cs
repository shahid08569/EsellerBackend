using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Orders.UpdateSellerOrderStatus;

public record UpdateSellerOrderStatusCommand(
    Guid SellerOrderId,
    OrderStatus NewStatus) : IRequest<Result>;