using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Wallet;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.ApproveProduct;

public class ApproveProductCommandHandler
    : IRequestHandler<ApproveProductCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public ApproveProductCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
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

        var shopkeeperAccountId = await _context.Shops
            .Where(s => s.Id == product.ShopId)
            .Join(_context.Shopkeepers, s => s.ShopkeeperId, sk => sk.Id, (s, sk) => sk.AccountId)
            .FirstOrDefaultAsync(cancellationToken);

        if (shopkeeperAccountId != Guid.Empty)
        {
            var notif = new Notification
            {
                AccountId = shopkeeperAccountId,
                Type = NotificationType.ProductApproved,
                Title = "Product approved",
                Message = $"Your product \"{product.Name}\" was approved and is now live.",
                IsRead = false
            };
            notif.SetCreatedAt(_dateTime.UtcNow);
            _context.Notifications.Add(notif);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}