using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.AnswerQuestion;

public class AnswerQuestionCommandHandler
    : IRequestHandler<AnswerQuestionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public AnswerQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        AnswerQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;
        var isShopkeeper = role == RoleType.Shopkeeper;

        if (!isAdmin && !isShopkeeper)
            return Result.Failure("Only shopkeepers or admins can answer.", "FORBIDDEN");

        var question = await _context.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question is null)
            return Result.Failure("Question not found.", "QUESTION_NOT_FOUND");

        Guid? shopkeeperId = null;

        if (isShopkeeper)
        {
            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == question.ProductId, cancellationToken);

            if (product is null)
                return Result.Failure("Product not found.", "PRODUCT_NOT_FOUND");

            var shop = await _context.Shops
                .FirstOrDefaultAsync(s => s.Id == product.ShopId, cancellationToken);

            var shopkeeper = shop is null ? null : await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == shop.ShopkeeperId, cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure("You do not have permission.", "FORBIDDEN");

            shopkeeperId = shopkeeper.Id;
        }

        question.Answer = request.Answer.Trim();
        question.AnsweredByShopkeeperId = shopkeeperId;
        question.AnsweredAt = _dateTime.UtcNow;
        question.IsApproved = false;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}