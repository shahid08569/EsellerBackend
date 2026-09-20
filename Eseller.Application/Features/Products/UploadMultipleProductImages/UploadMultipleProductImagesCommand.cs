using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.UploadMultipleProductImages;

public class UploadMultipleProductImagesCommand : IRequest<Result<List<Guid>>>
{
    public Guid ProductId { get; set; }
    public List<ProductImageFile> Files { get; set; } = new();
}

public class ProductImageFile
{
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}