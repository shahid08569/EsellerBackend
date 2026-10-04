using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Banners.DeleteBanner;

public class DeleteBannerCommandHandler
    : IRequestHandler<DeleteBannerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorage;

    public DeleteBannerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorage)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
    }

    public async Task<Result> Handle(
        DeleteBannerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can delete banners.", "FORBIDDEN");

        var banner = await _context.HomepageBanners
            .FirstOrDefaultAsync(b => b.Id == request.BannerId, cancellationToken);

        if (banner is null)
            return Result.Failure("Banner not found.", "BANNER_NOT_FOUND");

        var imageUrl = banner.ImageUrl;

        _context.HomepageBanners.Remove(banner);
        await _context.SaveChangesAsync(cancellationToken);

        // Best-effort: remove uploaded image from disk after DB row is gone
        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _fileStorage.DeleteAsync(imageUrl, cancellationToken);
            }
            catch
            {
                // DB delete already succeeded — ignore storage cleanup failures
            }
        }

        return Result.Success();
    }
}
