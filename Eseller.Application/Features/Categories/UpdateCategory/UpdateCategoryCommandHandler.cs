using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.UpdateCategory;

public class UpdateCategoryCommandHandler
    : IRequestHandler<UpdateCategoryCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public UpdateCategoryCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure("Category not found.", "CATEGORY_NOT_FOUND");

        category.Name = request.Name;
        category.SeoTitle = request.SeoTitle;
        category.SeoDescription = request.SeoDescription;
        // Only flip active when explicitly provided (prevents accidental homepage hide)
        if (request.IsActive.HasValue)
            category.IsActive = request.IsActive.Value;
        category.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}