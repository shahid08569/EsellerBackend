using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.ProcessWithdrawal;

public record ProcessWithdrawalCommand(Guid WithdrawalId) : IRequest<Result>;