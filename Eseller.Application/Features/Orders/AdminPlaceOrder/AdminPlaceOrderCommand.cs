using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.AdminPlaceOrder;

public record AdminPlaceOrderCommand(
    Guid ProductVariantId,
    int Quantity,
    string? CustomerName,
    string? CustomerPhone,
    string? ShippingAddress,
    string? City,
    string? State,
    string? Country,
    string? OrderNotes) : IRequest<Result<Guid>>;
