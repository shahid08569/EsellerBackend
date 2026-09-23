using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Cms.UpdateCmsPage;

public class UpdateCmsPageCommandHandler
    : IRequestHandler<UpdateCmsPageCommand, Result>
{
    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public UpdateCmsPageCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result> Handle(
        UpdateCmsPageCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.AccountId is null)
            return Result.Failure("User is not authenticated.", "UNAUTHENTICATED");

        var role = _currentUserService.RoleType;
        if (role != RoleType.SuperAdmin && role != RoleType.Partner)
            return Result.Failure("Only admins can update CMS pages.", "FORBIDDEN");

        var page = await _context.CmsPages
            .FirstOrDefaultAsync(p => p.Id == request.PageId, cancellationToken);

        if (page is null)
            return Result.Failure("CMS page not found.", "PAGE_NOT_FOUND");

        page.Title = request.Title.Trim();
        page.Content = request.Content;
        page.SetUpdatedAt(_dateTime.UtcNow);

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}