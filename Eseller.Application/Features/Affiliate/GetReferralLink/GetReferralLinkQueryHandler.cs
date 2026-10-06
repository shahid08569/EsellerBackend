using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetReferralLink;

public class GetReferralLinkQueryHandler
    : IRequestHandler<GetReferralLinkQuery, Result<ReferralLinkDto>>
{
    private const string BaseUrl = "https://esellerglobal.com";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetReferralLinkQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<ReferralLinkDto>> Handle(
        GetReferralLinkQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<ReferralLinkDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Result.Failure<ReferralLinkDto>(
                "Only affiliates can access this.", "FORBIDDEN");

        var affiliate = await _context.Affiliates
            .FirstOrDefaultAsync(a => a.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (affiliate is null)
            return Result.Failure<ReferralLinkDto>(
                "Affiliate not found.", "AFFILIATE_NOT_FOUND");

        if (!affiliate.IsActive)
            return Result.Failure<ReferralLinkDto>(
                "Affiliate account is not active.", "AFFILIATE_INACTIVE");

        var baseLink = $"{BaseUrl}?ref={affiliate.ReferralCode}";

        var dto = new ReferralLinkDto(
            affiliate.ReferralCode,
            baseLink,
            null,
            null,
            null,
            null);

        return Result.Success(dto);
    }
}