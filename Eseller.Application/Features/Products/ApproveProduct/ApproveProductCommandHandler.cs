using Eseller.Application.Abstractions;
using Eseller.Application.Common;
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

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}