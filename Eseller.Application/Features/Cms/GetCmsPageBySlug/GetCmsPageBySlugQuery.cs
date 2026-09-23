using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Cms.GetCmsPageBySlug;

public record GetCmsPageBySlugQuery(string Slug) : IRequest<Result<CmsPageDto>>;