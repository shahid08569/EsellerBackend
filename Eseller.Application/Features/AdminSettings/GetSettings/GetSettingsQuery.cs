using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminSettings.GetSettings;

public record GetSettingsQuery(
    string? KeySearch = null,
    bool? IsActive = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedList<SettingDto>>>;