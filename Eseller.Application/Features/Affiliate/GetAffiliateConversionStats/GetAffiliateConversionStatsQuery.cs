using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateConversionStats;

public record GetAffiliateConversionStatsQuery : IRequest<Result<AffiliateConversionStatsDto>>;