using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.ApproveAffiliateCommission;

public record ApproveAffiliateCommissionCommand(Guid CommissionId) : IRequest<Result>;