using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.UploadCategoryImage;

public class UploadCategoryImageCommand : IRequest<Result<string>>
{
    public Guid CategoryId { get; set; }
    public Stream FileStream { get; set; } = Stream.Null;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long FileSize { get; set; }
}