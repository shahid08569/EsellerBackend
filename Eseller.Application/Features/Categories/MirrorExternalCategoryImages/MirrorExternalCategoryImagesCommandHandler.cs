using Eseller.Application.Abstractions;
using Eseller.Application.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Eseller.Application.Features.Categories.MirrorExternalCategoryImages;

public class MirrorExternalCategoryImagesCommandHandler
    : IRequestHandler<MirrorExternalCategoryImagesCommand, Result<MirrorExternalCategoryImagesResult>>
{
    private readonly IApplicationDbContext _context;
    private readonly IRemoteImageIngestService _ingest;
    private readonly IDateTime _dateTime;
    private readonly ILogger<MirrorExternalCategoryImagesCommandHandler> _logger;

    public MirrorExternalCategoryImagesCommandHandler(
        IApplicationDbContext context,
        IRemoteImageIngestService ingest,
        IDateTime dateTime,
        ILogger<MirrorExternalCategoryImagesCommandHandler> logger)
    {
        _context = context;
        _ingest = ingest;
        _dateTime = dateTime;
        _logger = logger;
    }

    public async Task<Result<MirrorExternalCategoryImagesResult>> Handle(
        MirrorExternalCategoryImagesCommand request,
        CancellationToken cancellationToken)
    {
        var categories = await _context.Categories
            .Where(c => c.DeletedAt == null && c.ImageUrl != null && c.ImageUrl != "")
            .ToListAsync(cancellationToken);

        var mirrored = 0;
        var skipped = 0;
        var failed = 0;
        var errors = new List<string>();

        foreach (var cat in categories)
        {
            var url = (cat.ImageUrl ?? string.Empty).Trim();
            try
            {
                var localUrl = await _ingest.IngestCategoryImageAsync(cat.Id, url, cancellationToken);
                if (string.IsNullOrWhiteSpace(localUrl))
                {
                    skipped++;
                    continue;
                }

                cat.ImageUrl = localUrl;
                cat.SetUpdatedAt(_dateTime.UtcNow);
                mirrored++;
            }
            catch (Exception ex)
            {
                failed++;
                errors.Add($"{cat.Name}: {ex.Message}");
                _logger.LogWarning(ex, "Failed to mirror category image for {CategoryId}", cat.Id);
            }
        }

        if (mirrored > 0)
            await _context.SaveChangesAsync(cancellationToken);

        return Result.Success(new MirrorExternalCategoryImagesResult(mirrored, skipped, failed, errors));
    }
}
