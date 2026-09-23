using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.FlashSales.DeleteFlashSale;

public class DeleteFlashSaleCommandHandler
    : IRequestHandler<DeleteFlashSaleCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteFlashSaleCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteFlashSaleCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can delete flash sales.", "FORBIDDEN");

        var flashSale = await _context.FlashSales
            .FirstOrDefaultAsync(f => f.Id == request.FlashSaleId, cancellationToken);

        if (flashSale is null)
            return Result.Failure("Flash sale not found.", "FLASHSALE_NOT_FOUND");

        flashSale.IsActive = false;
        flashSale.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}