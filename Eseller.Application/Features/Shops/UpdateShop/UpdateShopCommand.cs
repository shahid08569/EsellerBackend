using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Shops.UpdateShop;

public record UpdateShopCommand(
    Guid ShopId,
    string Name,
    string? Description,
    string? Phone,
    string? Address,
    string? City,
    string? Country) : IRequest<Result>;