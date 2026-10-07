using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Enums;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.CreatePaymentLogo;

public class CreatePaymentLogoCommandHandler
    : IRequestHandler<CreatePaymentLogoCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreatePaymentLogoCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreatePaymentLogoCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>("Only admins can create payment logos.", "FORBIDDEN");

        if (string.IsNullOrWhiteSpace(request.ImageUrl))
            return Result.Failure<Guid>("Image URL is required.", "VALIDATION");

        var name = string.IsNullOrWhiteSpace(request.Name)
            ? "Payment method"
            : request.Name.Trim();
        if (name.Length > 120) name = name[..120];

        var imageUrl = request.ImageUrl.Trim();
        if (imageUrl.Length > 500) imageUrl = imageUrl[..500];

        var now = _dateTime.UtcNow;
        var logo = new PaymentShowcaseLogo
        {
            Name = name,
            ImageUrl = imageUrl,
            SortOrder = request.SortOrder,
            IsActive = request.IsActive
        };
        logo.SetCreatedAt(now);
        logo.SetUpdatedAt(now);

        _context.PaymentShowcaseLogos.Add(logo);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(logo.Id);
    }
}
