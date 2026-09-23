using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.DeleteBlogPost;

public record DeleteBlogPostCommand(Guid PostId) : IRequest<Result>;