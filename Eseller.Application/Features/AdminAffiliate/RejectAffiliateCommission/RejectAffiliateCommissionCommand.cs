using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.RejectAffiliateCommission;

public record RejectAffiliateCommissionCommand(Guid CommissionId) : IRequest<Result>;