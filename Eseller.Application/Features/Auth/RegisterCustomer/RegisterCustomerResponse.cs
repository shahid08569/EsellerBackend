namespace Eseller.Application.Features.Auth.RegisterCustomer;

public record RegisterCustomerResponse(
    Guid AccountId,
    string Name,
    string Email,
    string RoleType);