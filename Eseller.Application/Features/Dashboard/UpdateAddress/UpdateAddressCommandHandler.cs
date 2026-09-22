using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.UpdateAddress;

public class UpdateAddressCommandHandler
    : IRequestHandler<UpdateAddressCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateAddressCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateAddressCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure("Only customers can update addresses.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure("User not found.", "USER_NOT_FOUND");

        var address = await _context.Addresses
            .FirstOrDefaultAsync(a => a.Id == request.AddressId && a.UserId == user.Id,
                cancellationToken);

        if (address is null)
            return Result.Failure("Address not found.", "ADDRESS_NOT_FOUND");

        // Handle default change
        if (request.IsDefault && !address.IsDefault)
        {
            var existingDefaults = await _context.Addresses
                .Where(a => a.UserId == user.Id && a.IsDefault && a.Id != address.Id)
                .ToListAsync(cancellationToken);

            foreach (var addr in existingDefaults)
                addr.IsDefault = false;
        }

        address.Label = request.Label;
        address.FullName = request.FullName;
        address.Phone = request.Phone;
        address.AddressLine1 = request.AddressLine1;
        address.AddressLine2 = request.AddressLine2;
        address.City = request.City;
        address.State = request.State;
        address.Country = request.Country;
        address.PostalCode = request.PostalCode;
        address.IsDefault = request.IsDefault;
        address.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}