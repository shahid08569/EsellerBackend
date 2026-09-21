namespace Eseller.Application.Features.Search;

public record AutocompleteDto(
    string Text,
    string Type,        // "Product", "Category", "Brand"
    Guid? Id,
    string? Slug,
    string? ImageUrl);