using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetReferralCode;

public record GetReferralCodeQuery : IRequest<Result<ReferralCodeDto>>;