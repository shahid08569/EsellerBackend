using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Orders.GetSellerOrders;

public record GetSellerOrdersQuery(
    int PageNumber = 1,
    int PageSize = 20,
    string? Status = null) : IRequest<Result<PagedList<SellerOrderListItemDto>>>;

public record SellerOrderListItemDto(
    Guid Id,
    Guid OrderRequestId,
    string OrderNumber,
    string CustomerName,
    string? CustomerPhone,
    string? ShippingAddress,
    string? City,
    string Status,
    string PaymentStatus,
    string PaymentMethod,
    decimal MerchantProfit,
    decimal TotalAmount,
    int TotalItems,
    List<SellerOrderListLineDto> Items,
    DateTime CreatedAt);

public record SellerOrderListLineDto(
    Guid Id,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal TotalPrice,
    string? ImageUrl);
