using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Blog.CreateBlogCategory;

public class CreateBlogCategoryCommandHandler
    : IRequestHandler<CreateBlogCategoryCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;

    public CreateBlogCategoryCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService)
    {
        _context = context;
        _currentUserService = currentUserService;
    }

    public async Task<Result<Guid>> Handle(
        CreateBlogCategoryCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>("Only admins can create categories.", "FORBIDDEN");

        var name = request.Name.Trim();
        var baseSlug = GenerateSlug(name);

        var slug = baseSlug;
        var counter = 1;

        while (await _context.BlogCategories.AnyAsync(c => c.Slug == slug, cancellationToken))
        {
            slug = $"{baseSlug}-{counter}";
            counter++;
        }

        var category = new BlogCategory
        {
            Name = name,
            Slug = slug
        };

        _context.BlogCategories.Add(category);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(category.Id);
    }

    private static string GenerateSlug(string name)
    {
        var slug = name.ToLowerInvariant().Trim()
            .Replace(" ", "-")
            .Replace("_", "-");

        slug = new string(slug.Where(c => char.IsLetterOrDigit(c) || c == '-').ToArray());

        while (slug.Contains("--"))
            slug = slug.Replace("--", "-");

        return slug.Trim('-');
    }
}