using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.Orders.GetAdminOrders;

public record GetAdminOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    OrderStatus? Status = null,
    Guid? ShopId = null,
    string? Search = null)
    : IRequest<Result<PagedList<AdminOrderListItemDto>>>;

public record AdminOrderListItemDto(
    Guid Id,
    string OrderNumber,
    Guid UserId,
    string CustomerName,
    string CustomerPhone,
    string CustomerEmail,
    string ShippingAddress,
    string City,
    string Status,
    int StatusValue,
    string PaymentStatus,
    string PaymentMethod,
    decimal TotalAmount,
    int TotalItems,
    string ShopName,
    Guid? ShopId,
    DateTime CreatedAt,
    List<OrderRequestItemDto> Items);
