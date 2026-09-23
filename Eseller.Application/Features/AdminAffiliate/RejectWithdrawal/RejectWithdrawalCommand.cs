using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.RejectWithdrawal;

public record RejectWithdrawalCommand(Guid WithdrawalId) : IRequest<Result>;