using Eseller.Application.Common;
using Eseller.Application.Features.Products;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetHomepageSections;

public record GetHomepageSectionsQuery(
    int Limit = 8) : IRequest<Result<HomepageSectionsDto>>;

public record HomepageSectionsDto(
    List<ProductListDto> Featured,
    List<ProductListDto> BestSelling,
    List<ProductListDto> HotSelling,
    List<ProductListDto> NewArrivals,
    List<ProductListDto> FlashSale,
    List<ProductListDto> LowStock,
    List<ProductListDto> OutOfStock,
List<HomepageCategorySectionDto> Categories);