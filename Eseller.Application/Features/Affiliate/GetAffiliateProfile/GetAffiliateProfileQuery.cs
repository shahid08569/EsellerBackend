using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateProfile;

public record GetAffiliateProfileQuery : IRequest<Result<AffiliateProfileDto>>;