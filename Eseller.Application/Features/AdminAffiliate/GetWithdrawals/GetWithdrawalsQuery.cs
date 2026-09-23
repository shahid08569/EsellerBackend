using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminAffiliate.GetWithdrawals;

public record GetWithdrawalsQuery(
    string? Status = null,
    Guid? AccountId = null,
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<AdminWithdrawalDto>>>;