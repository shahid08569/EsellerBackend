using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.AddCatalogProductToShop;

public record AddCatalogProductToShopCommand(
    Guid SourceProductId,
    Guid ShopId) : IRequest<Result<Guid>>;
