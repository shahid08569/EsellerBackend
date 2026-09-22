using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GetWithdrawals;

public record GetWithdrawalsQuery(
    string? Status = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<WithdrawalDto>>>;