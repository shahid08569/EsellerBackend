using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.GetAddresses;

public class GetAddressesQueryHandler
    : IRequestHandler<GetAddressesQuery, Result<IReadOnlyList<AddressDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAddressesQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<IReadOnlyList<AddressDto>>> Handle(
        GetAddressesQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<IReadOnlyList<AddressDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<IReadOnlyList<AddressDto>>(
                "Only customers can access addresses.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<IReadOnlyList<AddressDto>>(
                "User not found.", "USER_NOT_FOUND");

        var addresses = await _context.Addresses
            .Where(a => a.UserId == user.Id && a.IsActive)
            .OrderByDescending(a => a.IsDefault)
            .ThenByDescending(a => a.CreatedAt)
            .Select(a => new AddressDto(
                a.Id,
                a.Label,
                a.FullName,
                a.Phone,
                a.AddressLine1,
                a.AddressLine2,
                a.City,
                a.State,
                a.Country,
                a.PostalCode,
                a.IsDefault,
                a.CreatedAt))
            .ToListAsync(cancellationToken);

        return Result.Success<IReadOnlyList<AddressDto>>(addresses);
    }
}