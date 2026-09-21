using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.CreateOrderRequest;

public record CreateOrderRequestCommand(
    string? ReferralCode) : IRequest<Result<Guid>>;