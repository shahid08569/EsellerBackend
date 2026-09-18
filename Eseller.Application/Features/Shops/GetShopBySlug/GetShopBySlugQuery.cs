using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.GetShopBySlug;

public record GetShopBySlugQuery(string Slug) : IRequest<Result<ShopDetailDto>>;