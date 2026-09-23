using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminAuditLogs.GetAuditLogs;

public class GetAuditLogsQueryHandler
    : IRequestHandler<GetAuditLogsQuery, Result<PagedList<AuditLogDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetAuditLogsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<AuditLogDto>>> Handle(
        GetAuditLogsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<AuditLogDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<AuditLogDto>>(
                "Only admins can view audit logs.", "FORBIDDEN");

        var query = _context.AuditLogs.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.ActorType))
        {
            var actorType = request.ActorType.Trim();
            query = query.Where(a => a.ActorType == actorType);
        }

        if (request.ActorAccountId.HasValue)
            query = query.Where(a => a.ActorAccountId == request.ActorAccountId.Value);

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            var action = request.Action.Trim().ToLowerInvariant();
            query = query.Where(a => a.Action.ToLower().Contains(action));
        }

        if (!string.IsNullOrWhiteSpace(request.EntityType))
        {
            var entityType = request.EntityType.Trim();
            query = query.Where(a => a.EntityType == entityType);
        }

        if (request.EntityId.HasValue)
            query = query.Where(a => a.EntityId == request.EntityId.Value);

        if (request.FromDate.HasValue)
            query = query.Where(a => a.CreatedAt >= request.FromDate.Value);

        if (request.ToDate.HasValue)
            query = query.Where(a => a.CreatedAt <= request.ToDate.Value);

        query = query.OrderByDescending(a => a.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var logs = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var actorIds = logs
            .Where(l => l.ActorAccountId.HasValue)
            .Select(l => l.ActorAccountId!.Value)
            .Distinct()
            .ToList();

        var accounts = await _context.Accounts
            .Where(a => actorIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id, a => a.Email ?? string.Empty, cancellationToken);

        var result = logs.Select(l => new AuditLogDto(
            l.Id,
            l.ActorAccountId,
            l.ActorAccountId.HasValue
                ? accounts.GetValueOrDefault(l.ActorAccountId.Value)
                : null,
            l.ActorType,
            l.Action,
            l.EntityType,
            l.EntityId,
            l.OldValues,
            l.NewValues,
            l.IpAddress,
            l.CreatedAt)).ToList();

        var pagedList = new PagedList<AuditLogDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}