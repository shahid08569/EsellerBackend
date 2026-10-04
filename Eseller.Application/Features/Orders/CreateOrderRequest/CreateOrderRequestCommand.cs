using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.CreateOrderRequest;

public record CreateOrderRequestCommand(
    string? ReferralCode,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null) : IRequest<Result<Guid>>;