using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Discounts.DeleteDiscount;

public record DeleteDiscountCommand(Guid DiscountId) : IRequest<Result>;