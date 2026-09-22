using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateClickStats;

public record GetAffiliateClickStatsQuery : IRequest<Result<AffiliateClickStatsDto>>;