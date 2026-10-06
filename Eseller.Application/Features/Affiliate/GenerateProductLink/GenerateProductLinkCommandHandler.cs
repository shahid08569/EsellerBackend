using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GenerateProductLink;

public class GenerateProductLinkCommandHandler
    : IRequestHandler<GenerateProductLinkCommand, Result<ReferralLinkDto>>
{
    private const string BaseUrl = "https://esellerglobal.com";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GenerateProductLinkCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ReferralLinkDto>> Handle(
        GenerateProductLinkCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<ReferralLinkDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<ReferralLinkDto>(
                "Only affiliates can generate links.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<ReferralLinkDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        if (!affiliate.IsActive)
            return Result.Failure<ReferralLinkDto>(
                "Affiliate account is not active.", "AFFILIATE_INACTIVE");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<ReferralLinkDto>(
                "Product not available.", "PRODUCT_NOT_AVAILABLE");

        var baseLink = $"{BaseUrl}?ref={affiliate.ReferralCode}";
        var productLink = $"{BaseUrl}/products/{product.Slug}?ref={affiliate.ReferralCode}";

        var dto = new ReferralLinkDto(
            affiliate.ReferralCode,
            baseLink,
            productLink,
            product.Id,
            product.Name,
            product.Slug);

        return Result.Success(dto);
    }
}