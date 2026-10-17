using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.ApproveQuestion;

public class ApproveQuestionCommandHandler
    : IRequestHandler<ApproveQuestionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public ApproveQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        ApproveQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can approve questions.", "FORBIDDEN");

        var question = await _context.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question is null)
            return Result.Failure("Question not found.", "QUESTION_NOT_FOUND");

        var product = await _context.Products.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == question.ProductId, cancellationToken);
        if (product != null && !await PartnerScopeHelper.IsShopPermittedForPartnerAsync(_context, _currentUserService, product.ShopId, cancellationToken))
            return Result.Failure("Forbidden: You can only approve questions for shops registered through your referral link.", "FORBIDDEN");

        question.IsApproved = true;
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}