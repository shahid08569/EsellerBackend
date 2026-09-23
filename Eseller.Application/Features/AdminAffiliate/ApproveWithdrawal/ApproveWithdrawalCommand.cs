using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.ApproveWithdrawal;

public record ApproveWithdrawalCommand(Guid WithdrawalId) : IRequest<Result>;