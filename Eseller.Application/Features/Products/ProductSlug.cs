using Eseller.Application.Abstractions;
using Eseller.Domain.Entities.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Eseller.Application.Features.Products;

internal static class ProductSlug
{
    public static string Normalize(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var collapsed = string.Join(
            "-",
            name.ToLowerInvariant()
                .Trim()
                .Replace('_', '-')
                .Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));

        var slug = new string(collapsed
            .Where(c => char.IsLetterOrDigit(c) || c == '-')
            .ToArray())
            .Trim('-');

        while (slug.Contains("--", StringComparison.Ordinal))
            slug = slug.Replace("--", "-", StringComparison.Ordinal);

        return slug;
    }

    /// <summary>
    /// Soft-deleted / inactive rows still occupy IX_Products_Slug.
    /// Rename them so a fresh catalog product can reclaim a clean slug when unused by an active row.
    /// </summary>
    public static async Task ReleaseInactiveSlugAsync(
        IApplicationDbContext context,
        string slug,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return;

        var blockers = await context.Products
            .IgnoreQueryFilters()
            .Where(p => p.Slug == slug && (!p.IsActive || p.DeletedAt != null))
            .ToListAsync(cancellationToken);

        if (blockers.Count == 0)
            return;

        var now = DateTime.UtcNow;
        foreach (var blocker in blockers)
        {
            var suffix = Guid.NewGuid().ToString("N")[..8];
            var released = $"{slug}-archived-{suffix}";
            blocker.Slug = released.Length <= 300 ? released : released[..300];
            blocker.SetUpdatedAt(now);
        }
    }

    public static async Task<string> GenerateUniqueAsync(
        IApplicationDbContext context,
        string name,
        string? prefix = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = Normalize(name);
        var baseSlug = string.IsNullOrWhiteSpace(prefix)
            ? normalized
            : Normalize($"{prefix}-{normalized}");

        if (string.IsNullOrWhiteSpace(baseSlug))
            baseSlug = $"product-{Guid.NewGuid():N}"[..16];

        if (baseSlug.Length > 280)
            baseSlug = baseSlug[..280].Trim('-');

        // Prefer a clean slug: free inactive holders of the exact base slug.
        await ReleaseInactiveSlugAsync(context, baseSlug, cancellationToken);

        var existing = await context.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(p => p.Slug == baseSlug || p.Slug.StartsWith(baseSlug + "-"))
            .Select(p => p.Slug)
            .ToListAsync(cancellationToken);

        var taken = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var local in context.Products.Local)
        {
            if (!string.IsNullOrWhiteSpace(local.Slug))
                taken.Add(local.Slug);
        }

        if (!taken.Contains(baseSlug))
            return baseSlug;

        var counter = 1;
        while (taken.Contains($"{baseSlug}-{counter}"))
            counter++;

        return $"{baseSlug}-{counter}";
    }

    public static string WithUniqueSuffix(string baseSlug)
    {
        var root = string.IsNullOrWhiteSpace(baseSlug) ? "product" : baseSlug;
        if (root.Length > 280)
            root = root[..280].Trim('-');

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var slug = $"{root}-{suffix}";
        return slug.Length <= 300 ? slug : slug[..300];
    }

    public static bool IsUniqueSlugViolation(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException!)
        {
            var message = current.Message ?? string.Empty;
            if (message.Contains("IX_Products_Slug", StringComparison.OrdinalIgnoreCase) ||
                (message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase) &&
                 message.Contains("Slug", StringComparison.OrdinalIgnoreCase)))
            {
                return true;
            }
        }

        return false;
    }
}
