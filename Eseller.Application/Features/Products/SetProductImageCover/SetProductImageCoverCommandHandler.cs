using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products.SetProductImageCover;

public class SetProductImageCoverCommandHandler
    : IRequestHandler<SetProductImageCoverCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public SetProductImageCoverCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        SetProductImageCoverCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only Super Admin can set cover images.", "FORBIDDEN");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId && p.DeletedAt == null, cancellationToken);

        if (product is null)
            return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

        var images = await _context.ProductImages
            .Where(pi => pi.ProductId == request.ProductId)
            .ToListAsync(cancellationToken);

        var cover = images.FirstOrDefault(pi => pi.Id == request.ImageId);
        if (cover is null)
            return Result.Failure("Image not found.", "IMAGE_NOT_FOUND");

        foreach (var img in images)
            img.IsCover = img.Id == request.ImageId;

        cover.SortOrder = 0;
        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
