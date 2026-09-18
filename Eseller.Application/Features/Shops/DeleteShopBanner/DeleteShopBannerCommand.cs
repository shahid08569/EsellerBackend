using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.DeleteShopBanner;

public record DeleteShopBannerCommand(Guid ShopId) : IRequest<Result>;