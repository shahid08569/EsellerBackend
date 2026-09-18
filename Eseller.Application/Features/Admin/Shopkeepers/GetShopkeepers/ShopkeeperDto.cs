namespace Eseller.Application.Features.Admin.Shopkeepers.GetShopkeepers;

public record ShopkeeperDto(
    Guid Id,
    Guid AccountId,
    string Name,
    string Email,
    string Phone,
    string StoreName,
    string StoreUrl,
    string City,
    string Country,
    string Status,
    DateTime? ApprovedAt,
    string? RejectionReason,
    DateTime CreatedAt);