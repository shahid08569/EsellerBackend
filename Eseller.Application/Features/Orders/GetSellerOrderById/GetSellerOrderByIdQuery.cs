using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetSellerOrderById;

public record GetSellerOrderByIdQuery(Guid SellerOrderId) : IRequest<Result<SellerOrderDto>>;