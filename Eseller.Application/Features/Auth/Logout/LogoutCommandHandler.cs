using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Auth.Logout;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IJwtService _jwtService;
    private readonly IDateTime _dateTime;

    public LogoutCommandHandler(
        IApplicationDbContext context,
        IJwtService jwtService,
        IDateTime dateTime)
    {
        _context = context;
        _jwtService = jwtService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Hash the incoming refresh token
        var tokenHash = _jwtService.HashToken(request.RefreshToken);

        // 2. Find the token
        var token = await _context.RefreshTokens
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, cancellationToken);

        // 3. If not found, still return success (idempotent logout)
        if (token is null)
        {
            return Result.Success();
        }

        // 4. If already revoked, still return success
        if (token.RevokedAt is not null)
        {
            return Result.Success();
        }

        // 5. Revoke the token
        token.Revoke(_dateTime.UtcNow);

        // 6. Save
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}