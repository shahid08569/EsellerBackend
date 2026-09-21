using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.RejectQuestion;

public class RejectQuestionCommandHandler
    : IRequestHandler<RejectQuestionCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public RejectQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        RejectQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can reject questions.", "FORBIDDEN");

        var question = await _context.ProductQuestions
            .FirstOrDefaultAsync(q => q.Id == request.QuestionId, cancellationToken);

        if (question is null)
            return Result.Failure("Question not found.", "QUESTION_NOT_FOUND");

        // ✅ SOFT DELETE (reject = soft delete)
        question.IsDeleted = true;
        question.DeletedAt = _dateTime.UtcNow;
        question.DeletedByAccountId = _currentUserService.AccountId.Value;
        question.IsApproved = false;

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}