using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.GetCategoryById;

public class GetCategoryByIdQueryHandler
    : IRequestHandler<GetCategoryByIdQuery, Result<CategoryDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCategoryByIdQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CategoryDto>> Handle(
        GetCategoryByIdQuery request,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure<CategoryDto>("Category not found.", "CATEGORY_NOT_FOUND");

        var dto = new CategoryDto(
            category.Id,
            category.ParentCategoryId,
            category.Name,
            category.Slug,
            category.ImageUrl,
            category.SeoTitle,
            category.SeoDescription,
            category.IsActive,
            category.CreatedAt);

        return Result.Success(dto);
    }
}