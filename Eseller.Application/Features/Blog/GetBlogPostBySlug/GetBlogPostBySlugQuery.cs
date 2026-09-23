using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.GetBlogPostBySlug;

public record GetBlogPostBySlugQuery(string Slug) : IRequest<Result<BlogPostDto>>;