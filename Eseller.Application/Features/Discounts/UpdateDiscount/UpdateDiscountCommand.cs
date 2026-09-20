using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Discounts.UpdateDiscount;

public record UpdateDiscountCommand(
    Guid DiscountId,
    DiscountType Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive) : IRequest<Result>;