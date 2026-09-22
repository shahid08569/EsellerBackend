using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.RequestWithdrawal;

public record RequestWithdrawalCommand(
    decimal Amount,
    string PayoutMethod) : IRequest<Result<Guid>>;