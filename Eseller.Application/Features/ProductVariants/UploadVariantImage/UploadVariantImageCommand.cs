using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.ProductVariants.UploadVariantImage;

public class UploadVariantImageCommand : IRequest<Result<string>>
{
    public Guid ProductId { get; set; }
    public Guid VariantId { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}