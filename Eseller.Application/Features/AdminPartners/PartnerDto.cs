namespace Eseller.Application.Features.AdminPartners;

public record PartnerDto(
    Guid Id,
    Guid AccountId,
    string Email,
    Guid AddedBySuperAdminId,
    string AddedByEmail,
    DateTime AddedAt,
    string? Permissions,
    DateTime CreatedAt,
    bool IsActive = true,
    string ReferralCode = "",
    int ReferredShopsCount = 0);