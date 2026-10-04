using Eseller.Application.Features.Products;

namespace Eseller.Application.Features.Homepage;

public record ProductSectionDto(
    string SectionName,
    int TotalCount,
    List<ProductListDto> Products);