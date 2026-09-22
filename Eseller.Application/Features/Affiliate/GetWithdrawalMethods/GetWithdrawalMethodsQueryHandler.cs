using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Affiliate.GetWithdrawalMethods;

public class GetWithdrawalMethodsQueryHandler
    : IRequestHandler<GetWithdrawalMethodsQuery, Result<List<string>>>
{
    private readonly ICurrentUserService _currentUserService;

    public GetWithdrawalMethodsQueryHandler(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public Task<Result<List<string>>> Handle(
        GetWithdrawalMethodsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Task.FromResult(Result.Failure<List<string>>(
                "User is not authenticated.", "UNAUTHENTICATED"));

        if (_currentUserService.RoleType != RoleType.Affiliate)
            return Task.FromResult(Result.Failure<List<string>>(
                "Only affiliates can access withdrawal methods.", "FORBIDDEN"));

        // Tere DB ke PayoutMethod (nvarchar) ke hisaab se
        var methods = new List<string> { "Bank", "JazzCash", "Easypaisa" };

        return Task.FromResult(Result.Success(methods));
    }
}