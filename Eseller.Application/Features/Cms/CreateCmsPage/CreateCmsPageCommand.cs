using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cms.CreateCmsPage;

public record CreateCmsPageCommand(
    string Slug,
    string Title,
    string Content) : IRequest<Result<Guid>>;