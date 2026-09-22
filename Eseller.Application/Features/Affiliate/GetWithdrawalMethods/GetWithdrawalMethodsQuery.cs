using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetWithdrawalMethods;

public record GetWithdrawalMethodsQuery : IRequest<Result<List<string>>>;