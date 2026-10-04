using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Orders.UpdateOrderPaymentStatus;

public record UpdateOrderPaymentStatusCommand(
    Guid OrderId,
    PaymentStatus NewStatus) : IRequest<Result>;
