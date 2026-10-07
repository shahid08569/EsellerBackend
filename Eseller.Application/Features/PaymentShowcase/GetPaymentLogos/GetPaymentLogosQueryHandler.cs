using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.PaymentShowcase.GetPaymentLogos;

public class GetPaymentLogosQueryHandler
    : IRequestHandler<GetPaymentLogosQuery, Result<List<PaymentShowcaseLogoDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPaymentLogosQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<List<PaymentShowcaseLogoDto>>> Handle(
        GetPaymentLogosQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<List<PaymentShowcaseLogoDto>>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<List<PaymentShowcaseLogoDto>>("Only admins can manage payment logos.", "FORBIDDEN");

        var query = _context.PaymentShowcaseLogos.AsNoTracking().AsQueryable();
        if (request.IsActive.HasValue)
            query = query.Where(x => x.IsActive == request.IsActive.Value);

        var logos = await query
            .OrderBy(x => x.SortOrder)
            .ThenByDescending(x => x.CreatedAt)
            .Select(x => new PaymentShowcaseLogoDto(
                x.Id,
                x.Name,
                x.ImageUrl,
                x.SortOrder,
                x.IsActive,
                x.CreatedAt,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success(logos);
    }
}
