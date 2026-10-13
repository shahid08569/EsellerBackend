using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.BuyNow;

public record BuyNowCommand(
    Guid ProductVariantId,
    int Quantity,
    string? ReferralCode = null,
    string? CustomerName = null,
    string? CustomerPhone = null,
    string? ShippingAddress = null,
    string? City = null,
    string? State = null,
    string? Country = null,
    string? OrderNotes = null) : IRequest<Result<Guid>>;