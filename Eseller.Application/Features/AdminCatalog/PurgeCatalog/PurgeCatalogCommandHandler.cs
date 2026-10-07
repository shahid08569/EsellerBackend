using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using Eseller.Domain.Enums;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.AdminCatalog.PurgeCatalog;

public class PurgeCatalogCommandHandler
    : IRequestHandler<PurgeCatalogCommand, Result<PurgeCatalogResult>>
{
    public const string ConfirmPhrase = "PURGE_CATALOG";

    private readonly IApplicationDbContext _context;
    private readonly ICurrentUserService _currentUserService;
    private readonly IDateTime _dateTime;

    public PurgeCatalogCommandHandler(
        IApplicationDbContext context,
        ICurrentUserService currentUserService,
        IDateTime dateTime)
    {
        _context = context;
        _currentUserService = currentUserService;
        _dateTime = dateTime;
    }

    public async Task<Result<PurgeCatalogResult>> Handle(
        PurgeCatalogCommand request,
        CancellationToken cancellationToken)
    {
        if (_currentUserService.RoleType != RoleType.SuperAdmin)
            return Result.Failure<PurgeCatalogResult>(
                "Only Super Admin can purge the catalog.",
                "FORBIDDEN");

        if (!string.Equals(request.Confirm?.Trim(), ConfirmPhrase, StringComparison.Ordinal))
            return Result.Failure<PurgeCatalogResult>(
                $"Send confirm: \"{ConfirmPhrase}\".",
                "CONFIRM_REQUIRED");

        var now = _dateTime.UtcNow;

        var products = await _context.Products
            .IgnoreQueryFilters()
            .Where(p => p.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var p in products)
        {
            p.IsActive = false;
            p.DeletedAt = now;
            p.SetUpdatedAt(now);
        }

        var brands = await _context.Brands
            .IgnoreQueryFilters()
            .Where(b => b.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var b in brands)
        {
            b.IsActive = false;
            b.DeletedAt = now;
            b.SetUpdatedAt(now);
        }

        var categories = await _context.Categories
            .IgnoreQueryFilters()
            .Where(c => c.DeletedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var c in categories)
        {
            c.IsActive = false;
            c.DeletedAt = now;
            c.SetUpdatedAt(now);
        }

        await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new PurgeCatalogResult(
            products.Count,
            brands.Count,
            categories.Count));
    }
}
