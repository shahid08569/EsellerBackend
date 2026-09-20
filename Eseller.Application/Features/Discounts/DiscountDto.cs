namespace Eseller.Application.Features.Discounts;

public record DiscountDto(
    Guid Id,
    Guid? ProductId,
    string? ProductName,
    string ScopeType,
    Guid? ScopeId,
    string? ScopeName,
    string Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    DateTime CreatedAt);