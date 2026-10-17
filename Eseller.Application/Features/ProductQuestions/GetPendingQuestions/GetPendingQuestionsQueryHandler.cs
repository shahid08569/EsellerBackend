using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.GetPendingQuestions;

public class GetPendingQuestionsQueryHandler
    : IRequestHandler<GetPendingQuestionsQuery, Result<PagedList<ProductQuestionDto>>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public GetPendingQuestionsQueryHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<PagedList<ProductQuestionDto>>> Handle(
        GetPendingQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<PagedList<ProductQuestionDto>>(
                "User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<PagedList<ProductQuestionDto>>(
                "Only admins can view pending questions.", "FORBIDDEN");

        var query = _context.ProductQuestions
            .Where(q => !q.IsApproved);

        var partnerShopIds = await PartnerScopeHelper.GetPartnerShopIdsAsync(_context, _currentUserService, cancellationToken);
        if (partnerShopIds != null)
        {
            var permittedProductIds = _context.Products.IgnoreQueryFilters().AsNoTracking()
                .Where(p => partnerShopIds.Contains(p.ShopId) && p.DeletedAt == null)
                .Select(p => p.Id);
            query = query.Where(q => permittedProductIds.Contains(q.ProductId));
        }

        query = query.OrderByDescending(q => q.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var questions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        var userIds = questions.Select(q => q.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        var answererIds = questions
            .Where(q => q.AnsweredByShopkeeperId.HasValue)
            .Select(q => q.AnsweredByShopkeeperId!.Value)
            .Distinct()
            .ToList();

        var answerers = await _context.Shopkeepers
            .Where(sk => answererIds.Contains(sk.Id))
            .ToDictionaryAsync(sk => sk.Id, sk => sk.Name, cancellationToken);

        var result = questions.Select(q => new ProductQuestionDto(
            q.Id,
            q.ProductId,
            q.UserId,
            users.GetValueOrDefault(q.UserId, string.Empty),
            q.Question,
            q.IsApproved,
            q.CreatedAt,
            q.Answer,
            q.AnsweredByShopkeeperId,
            q.AnsweredByShopkeeperId.HasValue
                ? answerers.GetValueOrDefault(q.AnsweredByShopkeeperId.Value, "Seller")
                : null,
            q.AnsweredAt))
            .ToList();

        var pagedList = new PagedList<ProductQuestionDto>(
            result, totalCount, request.PageNumber, request.PageSize);

        return Result.Success(pagedList);
    }
}