using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Discounts.GetDiscountById;

public record GetDiscountByIdQuery(Guid DiscountId) : IRequest<Result<DiscountDto>>;