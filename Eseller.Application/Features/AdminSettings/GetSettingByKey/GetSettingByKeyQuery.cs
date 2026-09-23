using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminSettings.GetSettingByKey;

public record GetSettingByKeyQuery(string Key) : IRequest<Result<SettingDto>>;