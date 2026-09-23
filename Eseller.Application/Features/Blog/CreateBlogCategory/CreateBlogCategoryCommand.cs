using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Blog.CreateBlogCategory;

public record CreateBlogCategoryCommand(string Name) : IRequest<Result<Guid>>;