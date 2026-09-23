using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminSettings.GetSettings;

public class GetSettingsQueryHandler
    : IRequestHandler<GetSettingsQuery, Result<PagedList<SettingDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSettingsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<SettingDto>>> Handle(
        GetSettingsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<SettingDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<SettingDto>>(
                "Only admins can view settings.", "FORBIDDEN");

        var query = _context.Settings.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.KeySearch))
        {
            var search = request.KeySearch.Trim().ToLowerInvariant();
            query = query.Where(s => s.Key.ToLower().Contains(search));
        }

        if (request.IsActive.HasValue)
            query = query.Where(s => s.IsActive == request.IsActive.Value);

        query = query.OrderBy(s => s.Key);

        var totalCount = await query.CountAsync(cancellationToken);

        var settings = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var result = settings.Select(s => new SettingDto(
            s.Id,
            s.Key,
            s.Value,
            s.Type,
            s.IsActive,
            s.CreatedAt,
            s.UpdatedAt)).ToList();

        var pagedList = new PagedList<SettingDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}