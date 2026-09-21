using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.BuyNow;

public record BuyNowCommand(
    Guid ProductVariantId,
    int Quantity,
    string? ReferralCode) : IRequest<Result<Guid>>;