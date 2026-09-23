using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminSettings.UpdateSetting;

public record UpdateSettingCommand(
    string Key,
    string Value,
    bool? IsActive = null) : IRequest<Result>;