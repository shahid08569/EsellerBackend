using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.PaymentShowcase.UpdatePaymentLogo;

public class UpdatePaymentLogoCommandHandler
    : IRequestHandler<UpdatePaymentLogoCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdatePaymentLogoCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdatePaymentLogoCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update payment logos.", "FORBIDDEN");

        var logo = await _context.PaymentShowcaseLogos
            .FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);

        if (logo is null)
            return Result.Failure("Payment logo not found.", "PAYMENT_LOGO_NOT_FOUND");

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            return Result.Failure("Image URL is required.", "VALIDATION");

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? "Payment method"
            : request.Name.Trim();
        if (name.Length > 120) name = name[..120];

        var imageUrl = request.ImageUrl.Trim();
        if (imageUrl.Length > 500) imageUrl = imageUrl[..500];

        logo.Name = name;
        logo.ImageUrl = imageUrl;
        logo.SortOrder = request.SortOrder;
        logo.IsActive = request.IsActive;
        logo.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
