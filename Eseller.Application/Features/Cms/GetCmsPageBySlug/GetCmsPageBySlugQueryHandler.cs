using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cms.GetCmsPageBySlug;

public class GetCmsPageBySlugQueryHandler
    : IRequestHandler<GetCmsPageBySlugQuery, Result<CmsPageDto>>
{
    private readonly IApplicationDbContext _context;

    public GetCmsPageBySlugQueryHandler(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<CmsPageDto>> Handle(
        GetCmsPageBySlugQuery request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Slug))
            return Result.Failure<CmsPageDto>("Slug is required.", "INVALID_SLUG");

        var slug = request.Slug.Trim().ToLowerInvariant();

        var page = await _context.CmsPages
            .FirstOrDefaultAsync(p => p.Slug == slug, cancellationToken);

        if (page is null)
            return Result.Failure<CmsPageDto>("CMS page not found.", "PAGE_NOT_FOUND");

        var dto = new CmsPageDto(
            page.Id,
            page.Slug,
            page.Title,
            page.Content,
            page.CreatedAt,
            page.UpdatedAt);

        return Result.Success(dto);
    }
}