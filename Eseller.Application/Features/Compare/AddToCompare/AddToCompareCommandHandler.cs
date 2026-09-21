using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Compare.AddToCompare;

public class AddToCompareCommandHandler
    : IRequestHandler<AddToCompareCommand, Result<Guid>>
{
    private const int MaxCompareItems = 4;

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AddToCompareCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        AddToCompareCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.User)
            return Result.Failure<Guid>("Only customers can use compare.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        var product = await _context.Products
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        // Duplicate check
        var existing = await _context.CompareItems
            .FirstOrDefaultAsync(c => c.UserId == user.Id && c.ProductId == request.ProductId,
                cancellationToken);

        if (existing is not null)
            return Result.Failure<Guid>("Product is already in compare list.", "ALREADY_IN_COMPARE");

        // Max limit check
        var currentCount = await _context.CompareItems
            .CountAsync(c => c.UserId == user.Id, cancellationToken);

        if (currentCount >= MaxCompareItems)
            return Result.Failure<Guid>(
                $"You can compare up to {MaxCompareItems} products only.",
                "COMPARE_LIMIT_EXCEEDED");

        var compareItem = new Domain.Entities.Engagement.CompareItem
        {
            UserId = user.Id,
            ProductId = request.ProductId,
            CreatedAt = _dateTime.UtcNow
        };

        _context.CompareItems.Add(compareItem);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(compareItem.Id);
    }
}