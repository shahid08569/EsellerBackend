using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.AdminPlaceOrder;

public record AdminPlaceOrderItemDto(
    Guid ProductVariantId,
    int Quantity);

public record AdminPlaceOrderCommand(
    Guid? ProductVariantId,
    int? Quantity,
    string? CustomerName,
    string? CustomerPhone,
    string? ShippingAddress,
    string? City,
    string? State,
    string? Country,
    string? OrderNotes,
    List<AdminPlaceOrderItemDto>? Items = null) : IRequest<Result<Guid>>;
