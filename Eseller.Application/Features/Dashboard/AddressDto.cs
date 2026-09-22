namespace Eseller.Application.Features.Dashboard;

public record AddressDto(
    Guid Id,
    string Label,
    string FullName,
    string Phone,
    string AddressLine1,
    string? AddressLine2,
    string City,
    string? State,
    string Country,
    string? PostalCode,
    bool IsDefault,
    DateTime CreatedAt);