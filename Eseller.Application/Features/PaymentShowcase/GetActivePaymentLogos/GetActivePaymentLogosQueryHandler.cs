using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.PaymentShowcase.GetActivePaymentLogos;

public class GetActivePaymentLogosQueryHandler
    : IRequestHandler<GetActivePaymentLogosQuery, Result<List<PaymentShowcaseLogoDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetActivePaymentLogosQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<List<PaymentShowcaseLogoDto>>> Handle(
        GetActivePaymentLogosQuery request,
        CancellationToken cancellationToken)
    {
        try
        {
            var logos = await _context.PaymentShowcaseLogos
                .AsNoTracking()
                .Where(x => x.IsActive)
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
        catch
        {
            // Table may not exist yet on a lagging deploy — never break homepage
            return Result.Success(new List<PaymentShowcaseLogoDto>());
        }
    }
}
