using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.ChangePassword;

public class ChangePasswordCommandHandler : IRequestHandler<ChangePasswordCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public ChangePasswordCommandHandler(
        IApplicationDbContext context,
        IPasswordHasher passwordHasher,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUserService.IsAuthenticated || _currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var accountId = _currentUserService.AccountId.Value;

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == accountId, cancellationToken);

        if (account is null)
            return Result.Failure("Account not found.", "ACCOUNT_NOT_FOUND");

        if (!_passwordHasher.Verify(request.CurrentPassword, account.PasswordHash))
            return Result.Failure("Current password is incorrect.", "INVALID_CURRENT_PASSWORD");

        account.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        account.SetUpdatedAt(_dateTime.UtcNow);

        var activeTokens = await _context.RefreshTokens
            .Where(rt => rt.AccountId == accountId && rt.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
            token.Revoke(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}