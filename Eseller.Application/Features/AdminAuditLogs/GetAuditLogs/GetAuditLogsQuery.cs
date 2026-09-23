using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAuditLogs.GetAuditLogs;

public record GetAuditLogsQuery(
    string? ActorType = null,
    Guid? ActorAccountId = null,
    string? Action = null,
    string? EntityType = null,
    Guid? EntityId = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    int PageNumber = 1,
    int PageSize = 50) : IRequest<Result<PagedList<AuditLogDto>>>;