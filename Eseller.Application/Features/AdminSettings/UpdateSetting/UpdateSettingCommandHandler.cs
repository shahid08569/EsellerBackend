using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminSettings.UpdateSetting;

public class UpdateSettingCommandHandler
    : IRequestHandler<UpdateSettingCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateSettingCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateSettingCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin)
            return Result.Failure(
                "Only SuperAdmin can update settings.", "FORBIDDEN");

        if (string.IsNullOrWhiteSpace(request.Key))
            return Result.Failure("Key is required.", "INVALID_KEY");

        var key = request.Key.Trim();

        var setting = await _context.Settings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting is null)
            return Result.Failure("Setting not found.", "SETTING_NOT_FOUND");

        // Validate value based on type
        var type = setting.Type?.ToLowerInvariant();

        if (type == "int" && !int.TryParse(request.Value, out _))
            return Result.Failure("Value must be a valid integer.", "INVALID_VALUE");

        if (type == "decimal" && !decimal.TryParse(request.Value, out _))
            return Result.Failure("Value must be a valid decimal.", "INVALID_VALUE");

        if (type == "bool"
            && request.Value.ToLowerInvariant() != "true"
            && request.Value.ToLowerInvariant() != "false")
        {
            return Result.Failure("Value must be true or false.", "INVALID_VALUE");
        }

        setting.Value = request.Value;

        if (request.IsActive.HasValue)
            setting.IsActive = request.IsActive.Value;

        setting.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}