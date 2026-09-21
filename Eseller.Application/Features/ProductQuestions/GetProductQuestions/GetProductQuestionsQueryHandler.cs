using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.GetProductQuestions;

public class GetProductQuestionsQueryHandler
    : IRequestHandler<GetProductQuestionsQuery, Result<PagedList<ProductQuestionDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetProductQuestionsQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<PagedList<ProductQuestionDto>>> Handle(
        GetProductQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        // 1. Verify product exists + public
        var productExists = await _context.Products
            .AnyAsync(p => p.Id == request.ProductId && p.IsActive && p.IsApproved,
                cancellationToken);

        if (!productExists)
            return Result.Failure<PagedList<ProductQuestionDto>>(
                "Product not found.", "PRODUCT_NOT_FOUND");

        // 2. Only approved questions
        var query = _context.ProductQuestions
      .Where(q => q.ProductId == request.ProductId
               && q.IsApproved
               && !q.IsDeleted)    
      .OrderByDescending(q => q.CreatedAt);

        var totalCount = await query.CountAsync(cancellationToken);

        var questions = await query
            .Skip((request.PageNumber - 1) * request.PageSize)
            .Take(request.PageSize)
            .ToListAsync(cancellationToken);

        // 3. Load users (question askers)
        var userIds = questions.Select(q => q.UserId).Distinct().ToList();
        var users = await _context.Users
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.Name, cancellationToken);

        // 4. Load answerers (shopkeepers)
        var answererIds = questions
            .Where(q => q.AnsweredByShopkeeperId.HasValue)
            .Select(q => q.AnsweredByShopkeeperId!.Value)
            .Distinct()
            .ToList();

        var answerers = await _context.Shopkeepers
            .Where(sk => answererIds.Contains(sk.Id))
            .ToDictionaryAsync(sk => sk.Id, sk => sk.Name, cancellationToken);

        // 5. Build DTOs
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