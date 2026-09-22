using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Identity;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Dashboard.CreateAddress;

public class CreateAddressCommandHandler
    : IRequestHandler<CreateAddressCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateAddressCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateAddressCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<Guid>("Only customers can add addresses.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        var existingCount = await _context.Addresses
            .CountAsync(a => a.UserId == user.Id && a.IsActive, cancellationToken);

        // First address is always default
        var isDefault = existingCount == 0 || request.IsDefault;

        // If default, unset other defaults
        if (isDefault)
        {
            var existingDefaults = await _context.Addresses
                .Where(a => a.UserId == user.Id && a.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var addr in existingDefaults)
                addr.IsDefault = false;
        }

        var address = new Address
        {
            UserId = user.Id,
            Label = request.Label,
            FullName = request.FullName,
            Phone = request.Phone,
            AddressLine1 = request.AddressLine1,
            AddressLine2 = request.AddressLine2,
            City = request.City,
            State = request.State,
            Country = request.Country,
            PostalCode = request.PostalCode,
            IsDefault = isDefault,
            IsActive = true
        };

        _context.Addresses.Add(address);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(address.Id);
    }
}