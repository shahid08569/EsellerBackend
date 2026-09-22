using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.DeleteAnswer;

public class DeleteAnswerCommandHandler
    : IRequestHandler<DeleteAnswerCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public DeleteAnswerCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result> Handle(
        DeleteAnswerCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        var question = await _context.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question is null || string.IsNullOrEmpty(question.Answer))
            return Result.Failure("Answer not found.", "ANSWER_NOT_FOUND");

        if (!isAdmin)
        {
            if (!question.AnsweredByShopkeeperId.HasValue)
                return Result.Failure("You do not have permission.", "FORBIDDEN");

            var shopkeeper = await _context.Shopkeepers
                .FirstOrDefaultAsync(sk => sk.Id == question.AnsweredByShopkeeperId.Value,
                    cancellationToken);

            if (shopkeeper is null || shopkeeper.AccountId != _currentUserService.AccountId.Value)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        question.Answer = null;
        question.AnsweredByShopkeeperId = null;
        question.AnsweredAt = null;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}