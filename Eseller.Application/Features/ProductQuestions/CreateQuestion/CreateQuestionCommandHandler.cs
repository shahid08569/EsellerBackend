using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Engagement;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.ProductQuestions.CreateQuestion;

public class CreateQuestionCommandHandler
    : IRequestHandler<CreateQuestionCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateQuestionCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateQuestionCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        if (_currentUserService.RoleType != RoleType.User)
            return Result.Failure<Guid>("Only customers can ask questions.", "FORBIDDEN");

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.AccountId == _currentUserService.AccountId.Value,
                cancellationToken);

        if (user is null)
            return Result.Failure<Guid>("User not found.", "USER_NOT_FOUND");

        var product = await _context.Products
            .FirstOrDefaultAsync(p => p.Id == request.ProductId
                                   && p.IsActive
                                   && p.IsApproved
                                   && p.DeletedAt == null,
                cancellationToken);

        if (product is null)
            return Result.Failure<Guid>("Product not available.", "PRODUCT_NOT_AVAILABLE");

        var recentDuplicate = await _context.ProductQuestions
            .AnyAsync(q => q.ProductId == request.ProductId
                        && q.UserId == user.Id
                        && q.Question == request.Question.Trim()
                        && q.CreatedAt > _dateTime.UtcNow.AddHours(-24),
                cancellationToken);

        if (recentDuplicate)
            return Result.Failure<Guid>(
                "You have already asked this question recently.",
                "DUPLICATE_QUESTION");

        var question = new ProductQuestion
        {
            ProductId = request.ProductId,
            UserId = user.Id,
            Question = request.Question.Trim(),
            IsApproved = false,
            CreatedAt = _dateTime.UtcNow
        };

        _context.ProductQuestions.Add(question);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(question.Id);
    }
}