using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.GetCategories;

public class GetCategoriesQueryHandler
    : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryTreeDto>>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoriesQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<IReadOnlyList<CategoryTreeDto>>> Handle(
        GetCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

        var tree = BuildTree(categories, null);

        return Result.Success<IReadOnlyList<CategoryTreeDto>>(tree);
    }

    private static List<CategoryTreeDto> BuildTree(
        List<Domain.Entities.Catalog.Category> categories,
        Guid? parentId)
    {
        return categories
            .Where(c => c.ParentCategoryId == parentId)
            .Select(c => new CategoryTreeDto(
                c.Id,
                c.Name,
                c.Slug,
                c.ImageUrl,
                BuildTree(categories, c.Id)))
            .ToList();
    }
}