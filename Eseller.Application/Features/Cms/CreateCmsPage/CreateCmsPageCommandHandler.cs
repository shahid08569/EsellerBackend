using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Entities.Content;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cms.CreateCmsPage;

public class CreateCmsPageCommandHandler
    : IRequestHandler<CreateCmsPageCommand, Result<Guid>>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public CreateCmsPageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<Guid>> Handle(
        CreateCmsPageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure<Guid>("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure<Guid>("Only admins can create CMS pages.", "FORBIDDEN");

        var slug = request.Slug.Trim().ToLowerInvariant();

        var exists = await _context.CmsPages
            .AnyAsync(p => p.Slug == slug, cancellationToken);

        if (exists)
            return Result.Failure<Guid>("Slug already exists.", "SLUG_EXISTS");

        var now = _dateTime.UtcNow;

        var page = new CmsPage
        {
            Slug = slug,
            Title = request.Title.Trim(),
            Content = request.Content
        };

        page.SetCreatedAt(now);
        page.SetUpdatedAt(now);

        _context.CmsPages.Add(page);
        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(page.Id);
    }
}