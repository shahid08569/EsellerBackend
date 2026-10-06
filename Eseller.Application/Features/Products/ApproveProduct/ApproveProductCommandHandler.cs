using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.ApproveProduct;

public class ApproveProductCommandHandler
    : IRequestHandler<ApproveProductCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public ApproveProductCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _dateTime = dateTime;
        _notifications = notifications;
    }

    public async Task<Result> Handle(
        ApproveProductCommand request,
        CancellationToken cancellationToken)
    {
        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);
        if (product is null)
            return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

        if (product.IsApproved)
            return Result.Failure("Product is already approved.", "ALREADY_APPROVED");

        product.Approve();
        product.SetUpdatedAt(_dateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        var shopkeeperAccountId = await _context.Shops
            .Where(s => s.Id == product.ShopId)
            .Join(_context.Shopkeepers, s => s.ShopkeeperId, sk => sk.Id, (s, sk) => sk.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shopkeeperAccountId != Guid.Empty)
        {
            await _notifications.NotifyAsync(
                shopkeeperAccountId,
                RoleType.Shopkeeper,
                NotificationType.ProductListingRequestUpdated,
                "Product Listing Request Updated",
                $"Your product \"{product.Name}\" was approved and is now live.",
                NotificationEntityType.Product,
                product.Id,
                "/products",
                $"ProductListingRequestUpdated:{product.Id:N}:Approved:Seller:{shopkeeperAccountId:N}",
                cancellationToken);
        }

        return Result.Success();
    }
}
