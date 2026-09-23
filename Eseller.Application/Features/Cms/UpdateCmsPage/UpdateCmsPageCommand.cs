using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cms.UpdateCmsPage;

public record UpdateCmsPageCommand(
    Guid PageId,
    string Title,
    string Content) : IRequest<Result>;