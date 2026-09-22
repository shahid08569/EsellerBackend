using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetOrderById;

public record GetOrderByIdQuery(Guid OrderId) : IRequest<Result<OrderRequestDto>>;