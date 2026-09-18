using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Categories.DeleteCategoryImage;

public class DeleteCategoryImageCommandHandler
    : IRequestHandler<DeleteCategoryImageCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly IFileStorageService _fileStorageService;
    private readonly IDateTime _dateTime;

    public DeleteCategoryImageCommandHandler(
        IApplicationDbContext context,
        IFileStorageService fileStorageService,
        IDateTime dateTime)
    {
        _context = context;
        _fileStorageService = fileStorageService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        DeleteCategoryImageCommand request,
        CancellationToken cancellationToken)
    {
        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        if (category is null)
            return Result.Failure("Category not found.", "CATEGORY_NOT_FOUND");

        if (!string.IsNullOrEmpty(category.ImageUrl))
        {
            await _fileStorageService.DeleteAsync(category.ImageUrl, cancellationToken);
            category.ImageUrl = null;
            category.SetUpdatedAt(_dateTime.UtcNow);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}