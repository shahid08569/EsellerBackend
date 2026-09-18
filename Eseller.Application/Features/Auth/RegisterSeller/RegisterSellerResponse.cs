namespace Eseller.Application.Features.Auth.RegisterSeller;

public record RegisterSellerResponse(
    Guid AccountId,
    Guid ShopkeeperId,
    Guid ShopId,
    string Name,
    string Email,
    string StoreName,
    string StoreUrl,
    string Status,
    string Message);