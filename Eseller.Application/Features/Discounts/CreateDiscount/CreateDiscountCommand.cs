using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Discounts.CreateDiscount;

public record CreateDiscountCommand(
    Guid? ProductId,
    DiscountScopeType ScopeType,
    Guid? ScopeId,
    DiscountType Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate) : IRequest<Result<Guid>>;