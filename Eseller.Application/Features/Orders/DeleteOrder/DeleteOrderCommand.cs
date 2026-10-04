using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.DeleteOrder;

public record DeleteOrderCommand(Guid OrderId) : IRequest<Result>;
