using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.MirrorExternalCategoryImages;

/// <summary>
/// Downloads category logos hosted on external CDNs and stores them under /uploads
/// so admin/storefront never depend on third-party hotlink availability.
/// </summary>
public record MirrorExternalCategoryImagesCommand : IRequest<Result<MirrorExternalCategoryImagesResult>>;

public record MirrorExternalCategoryImagesResult(int Mirrored, int Skipped, int Failed, IReadOnlyList<string> Errors);
