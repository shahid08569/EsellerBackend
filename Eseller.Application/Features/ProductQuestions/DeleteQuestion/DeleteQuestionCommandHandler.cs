using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.DeleteQuestion;

public class DeleteQuestionCommandHandler
    : IRequestHandler<DeleteQuestionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public DeleteQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        var isAdmin = role == RoleType.SuperAdmin || role == RoleType.Partner;

        var question = await _context.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question is null)
            return Result.Failure("Question not found.", "QUESTION_NOT_FOUND");

        if (!isAdmin)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                    cancellationToken);

            if (user is null || question.UserId != user.Id)
                return Result.Failure("You do not have permission.", "FORBIDDEN");
        }

        // ✅ SOFT DELETE
        question.IsDeleted = true;
        question.DeletedAt = _dateTime.UtcNow;
        question.DeletedByAccountId = _currentUserService.AccountId.Value;
        question.IsApproved = false;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}