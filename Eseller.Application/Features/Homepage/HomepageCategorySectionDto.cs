using Eseller.Application.Features.Products;

namespace Eseller.Application.Features.Homepage;

public record HomepageCategorySectionDto(
    Guid Id,
    string Name,
    string Slug,
    string? ImageUrl,
    int DisplayOrder,
    List<ProductListDto> Products);