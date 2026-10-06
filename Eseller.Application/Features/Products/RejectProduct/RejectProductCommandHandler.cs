using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.RejectProduct;

public class RejectProductCommandHandler
    : IRequestHandler<RejectProductCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public RejectProductCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _dateTime = dateTime;
        _notifications = notifications;
    }

    public async Task<Result> Handle(
        RejectProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

        product.Reject(request.Reason);
        product.SetUpdatedAt(_dateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        var shopkeeperAccountId = await _context.Shops
            .Where(s => s.Id == product.ShopId)
            .Join(_context.Shopkeepers, s => s.ShopkeeperId, sk => sk.Id, (s, sk) => sk.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shopkeeperAccountId != Guid.Empty)
        {
            var reason = string.IsNullOrWhiteSpace(request.Reason) ? "No reason provided." : request.Reason.Trim();
            await _notifications.NotifyAsync(
                shopkeeperAccountId,
                RoleType.Shopkeeper,
                NotificationType.ProductListingRequestUpdated,
                "Product Listing Request Updated",
                $"Your product \"{product.Name}\" was rejected. Reason: {reason}",
                NotificationEntityType.Product,
                product.Id,
                "/products",
                $"ProductListingRequestUpdated:{product.Id:N}:Rejected:Seller:{shopkeeperAccountId:N}",
                cancellationToken);
        }

        return Result.Success();
    }
}
