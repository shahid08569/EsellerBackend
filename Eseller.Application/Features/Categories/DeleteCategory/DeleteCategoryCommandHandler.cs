using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.DeleteCategory;

public class DeleteCategoryCommandHandler
    : IRequestHandler<DeleteCategoryCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public DeleteCategoryCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteCategoryCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure("Category not found.", "CATEGORY_NOT_FOUND");

        // ⚠️ Check if category has child categories
        var hasChildren = await _context.Categories
            .AnyAsync(c => c.ParentCategoryId == request.CategoryId, cancellationToken);

        if (hasChildren)
            return Result.Failure(
                "Cannot delete category with subcategories. Delete subcategories first.",
                "CATEGORY_HAS_CHILDREN");

        // ⚠️ Check if category has products
        var hasProducts = await _context.Products
            .AnyAsync(p => p.CategoryId == request.CategoryId, cancellationToken);

        if (hasProducts)
            return Result.Failure(
                "Cannot delete category with products. Move or delete products first.",
                "CATEGORY_HAS_PRODUCTS");

        // Delete image if exists
        if (!string.IsNullOrEmpty(category.ImageUrl))
        {
            await _fileStorageService.DeleteAsync(category.ImageUrl, cancellationToken);
        }

        // Soft delete
        category.IsActive = false;
        category.DeletedAt = _dateTime.UtcNow;
        category.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}