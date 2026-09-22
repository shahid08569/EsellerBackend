using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.GetAffiliateCommissions;

public record GetAffiliateCommissionsQuery(
    string? Status = null,
    Guid? AffiliateId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<AffiliateCommissionDto>>>;