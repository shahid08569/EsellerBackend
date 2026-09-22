using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Affiliate;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.TrackClick;

public class TrackClickCommandHandler
    : IRequestHandler<TrackClickCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public TrackClickCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        TrackClickCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ReferralCode))
            return Result.Failure<Guid>("Referral code is required.", "INVALID_CODE");

        var normalizedCode = request.ReferralCode.Trim().ToUpperInvariant();

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.ReferralCode == normalizedCode, cancellationToken);

        if (affiliate is null)
            return Result.Failure<Guid>("Invalid referral code.", "INVALID_CODE");

        if (!affiliate.IsActive)
            return Result.Failure<Guid>("Affiliate is not active.", "AFFILIATE_INACTIVE");

        // Validate product if provided
        if (request.ProductId.HasValue)
        {
            var productExists = await _context.Products
                .AnyAsync(p => p.Id == request.ProductId.Value
                            && p.IsActive
                            && p.IsApproved
                            && p.DeletedAt == null,
                    cancellationToken);

            if (!productExists)
                return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");
        }

        var now = _dateTime.UtcNow;

        var click = new AffiliateClick
        {
            AffiliateId = affiliate.Id,
            ProductId = request.ProductId,
            ClickedAt = now,
            IpAddress = request.IpAddress?.Trim(),
            UserAgent = request.UserAgent?.Trim(),
            Converted = false
        };

        _context.AffiliateClicks.Add(click);

        // Update affiliate total clicks
        affiliate.TotalClicks += 1;
        affiliate.SetUpdatedAt(now);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(click.Id);
    }
}