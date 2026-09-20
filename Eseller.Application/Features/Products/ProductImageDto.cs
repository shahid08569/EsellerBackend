namespace Eseller.Application.Features.Products;

public record ProductImageDto(
    Guid Id,
    Guid ProductId,
    string ImageUrl,
    int SortOrder);