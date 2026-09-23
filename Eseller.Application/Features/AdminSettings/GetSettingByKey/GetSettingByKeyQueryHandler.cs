using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminSettings.GetSettingByKey;

public class GetSettingByKeyQueryHandler
    : IRequestHandler<GetSettingByKeyQuery, Result<SettingDto>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetSettingByKeyQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<SettingDto>> Handle(
        GetSettingByKeyQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<SettingDto>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<SettingDto>(
                "Only admins can view settings.", "FORBIDDEN");

        if (string.IsNullOrWhiteSpace(request.Key))
            return Result.Failure<SettingDto>("Key is required.", "INVALID_KEY");

        var key = request.Key.Trim();

        var setting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
            return Result.Failure<SettingDto>("Setting not found.", "SETTING_NOT_FOUND");

        var dto = new SettingDto(
            setting.Id,
            setting.Key,
            setting.Value,
            setting.Type,
            setting.IsActive,
            setting.CreatedAt,
            setting.UpdatedAt);

        return Result.Success(dto);
    }
}