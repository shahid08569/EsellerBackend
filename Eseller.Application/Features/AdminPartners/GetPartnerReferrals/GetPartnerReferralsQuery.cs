using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.GetPartnerReferrals;

public record GetPartnerReferralsQuery(Guid? PartnerId = null) : IRequest<Result<PartnerReferralsDto>>;

public record PartnerReferralsDto(
    Guid PartnerId,
    string Email,
    string ReferralCode,
    string ReferralLink,
    string AlternateLink,
    int TotalShops,
    int ActiveShops,
    int PendingShops,
    int SuspendedShops,
    int TotalOrders,
    decimal TotalSales,
    List<PartnerReferredShopDto> Shops);

public record PartnerReferredShopDto(
    Guid Id,
    Guid ShopkeeperId,
    string Name,
    string Slug,
    string? LogoUrl,
    string? Phone,
    string OwnerName,
    string OwnerEmail,
    string Status,
    bool IsActive,
    int TotalProducts,
    decimal Rating,
    int OrdersCount,
    decimal TotalSales,
    DateTime JoinedAt);
