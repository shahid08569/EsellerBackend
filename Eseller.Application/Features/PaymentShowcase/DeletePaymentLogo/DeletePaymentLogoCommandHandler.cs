using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.PaymentShowcase.DeletePaymentLogo;

public class DeletePaymentLogoCommandHandler
    : IRequestHandler<DeletePaymentLogoCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFileStorageService _fileStorage;

    public DeletePaymentLogoCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IFileStorageService fileStorage)
    {
        _context = context;
        _currentUserService = currentUserService;
        _fileStorage = fileStorage;
    }

    public async Task<Result> Handle(
        DeletePaymentLogoCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can delete payment logos.", "FORBIDDEN");

        var logo = await _context.PaymentShowcaseLogos
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (logo is null)
            return Result.Failure("Payment logo not found.", "PAYMENT_LOGO_NOT_FOUND");

        var imageUrl = logo.ImageUrl;
        _context.PaymentShowcaseLogos.Remove(logo);
        await _context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(imageUrl) &&
            imageUrl.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                await _fileStorage.DeleteAsync(imageUrl, cancellationToken);
            }
            catch
            {
                // DB delete already succeeded
            }
        }

        return Result.Success();
    }
}
