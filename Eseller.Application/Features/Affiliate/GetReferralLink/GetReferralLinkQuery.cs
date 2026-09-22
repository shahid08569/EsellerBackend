using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetReferralLink;

public record GetReferralLinkQuery : IRequest<Result<ReferralLinkDto>>;