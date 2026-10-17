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
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;
    private readonly IAppNotificationService _notifications;

    public ApproveProductCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime,
        IAppNotificationService notifications)
    {
        _context = context;
        _currentUserService = currentUserService;
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

        if (!await PartnerScopeHelper.IsShopPermittedForPartnerAsync(_context, _currentUserService, product.ShopId, cancellationToken))
            return Result.Failure("Forbidden: You can only approve products for shops registered through your referral link.", "FORBIDDEN");

        product.Approve();
        product.SetUpdatedAt(_dateTime.UtcNow);
        await _context.SaveChangesAsync(cancellationToken);

        var shopkeeperAccountId = await _context.Shops
            .Where(s => s.Id == product.ShopId)
            .Join(_context.Shopkeepers, s => s.ShopkeeperId, sk => sk.Id, (s, sk) => sk.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shopkeeperAccountId != Guid.Empty)
        {
            var isSellerOwn = product.SourceProductId is null;
            var body = isSellerOwn
                ? $"Your product \"{product.Name}\" was approved and is now in My Own Products (not added to global warehouse)."
                : $"Your warehouse listing \"{product.Name}\" was approved and is now live in your shop.";

            await _notifications.NotifyAsync(
                shopkeeperAccountId,
                RoleType.Shopkeeper,
                NotificationType.ProductListingRequestUpdated,
                "Product Listing Request Updated",
                body,
                NotificationEntityType.Product,
                product.Id,
                "/products",
                $"ProductListingRequestUpdated:{product.Id:N}:Approved:Seller:{shopkeeperAccountId:N}",
                cancellationToken);
        }

        return Result.Success();
    }
}
