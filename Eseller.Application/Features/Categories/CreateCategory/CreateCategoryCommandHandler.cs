using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Catalog;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.CreateCategory;

public class CreateCategoryCommandHandler
    : IRequestHandler<CreateCategoryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly IDateTime _dateTime;

    public CreateCategoryCommandHandler(
        IApplicationDbContext context,
        IDateTime dateTime)
    {
        _context = context;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateCategoryCommand request,
        CancellationToken cancellationToken)
    {
        // 1. Verify parent category exists (if provided)
        if (request.ParentCategoryId.HasValue)
        {
            var parentExists = await _context.Categories
                .AnyAsync(c => c.Id == request.ParentCategoryId.Value, cancellationToken);

            if (!parentExists)
                return Result.Failure<Guid>(
                    "Parent category not found.",
                    "PARENT_CATEGORY_NOT_FOUND");
        }

        // 2. Generate slug
        var slug = GenerateSlug(request.Name);

        // 3. Check slug uniqueness
        var slugExists = await _context.Categories
            .AnyAsync(c => c.Slug == slug, cancellationToken);

        if (slugExists)
            return Result.Failure<Guid>(
                "A category with this name already exists.",
                "CATEGORY_NAME_TAKEN");

        // 4. Create category
        var category = new Category
        {
            ParentCategoryId = request.ParentCategoryId,
            Name = request.Name,
            Slug = slug,
            SeoTitle = request.SeoTitle,
            SeoDescription = request.SeoDescription,
            IsActive = true
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }

    private static string GenerateSlug(string name)
    {
        return name
            .ToLowerInvariant()
            .Trim()
            .Replace(" ", "-")
            .Replace("_", "-")
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .Aggregate(string.Empty, (current, c) => current + c)
            .Replace("--", "-");
    }
}