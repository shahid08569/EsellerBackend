namespace Eseller.Application.Features.Auth.RegisterSeller;

public record RegisterSellerResponse(
    Guid AccountId,
    string Name,
    string Email,
    string RoleType,
    string Status,
    string Message);