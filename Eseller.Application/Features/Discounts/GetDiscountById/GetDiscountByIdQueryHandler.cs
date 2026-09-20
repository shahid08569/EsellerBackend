using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Discounts.GetDiscountById;

public class GetDiscountByIdQueryHandler
    : IRequestHandler<GetDiscountByIdQuery, Result<DiscountDto>>
{
    private readonly IApplicationDbContext _context;

    public GetDiscountByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<DiscountDto>> Handle(
        GetDiscountByIdQuery request,
        CancellationToken cancellationToken)
    {
        var discount = await _context.Discounts
            .Where(d => d.Id == request.DiscountId)
            .Select(d => new DiscountDto(
                d.Id,
                d.ProductId,
                d.ProductId == null
                    ? null
                    : _context.Products.Where(p => p.Id == d.ProductId).Select(p => p.Name).FirstOrDefault(),
                d.ScopeType.ToString(),
                d.ScopeId,
                null,   // ScopeName — baad mein resolve karenge
                d.Type.ToString(),
                d.Value,
                d.StartDate,
                d.EndDate,
                d.IsActive,
                d.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);

        if (discount is null)
            return Result.Failure<DiscountDto>("Discount not found.", "DISCOUNT_NOT_FOUND");

        return Result.Success(discount);
    }
}