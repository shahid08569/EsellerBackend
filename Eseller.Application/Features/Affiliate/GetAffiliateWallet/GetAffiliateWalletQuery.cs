using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetAffiliateWallet;

public record GetAffiliateWalletQuery : IRequest<Result<WalletDto>>;