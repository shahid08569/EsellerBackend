using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.UploadProductImage;

public class UploadProductImageCommand : IRequest<Result<Guid>>
{
    public Guid ProductId { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}