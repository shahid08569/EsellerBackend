namespace Eseller.Application.Features.Discounts;

public record DiscountListDto(
    Guid Id,
    Guid? ProductId,
    string? ProductName,
    string ScopeType,
    Guid? ScopeId,
    string Type,
    decimal Value,
    DateTime StartDate,
    DateTime EndDate,
    bool IsActive,
    bool IsCurrentlyActive);