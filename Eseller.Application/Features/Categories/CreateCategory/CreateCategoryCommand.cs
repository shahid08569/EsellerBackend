using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.CreateCategory;

public record CreateCategoryCommand(
    Guid? ParentCategoryId,
    string Name,
    string? SeoTitle,
    string? SeoDescription) : IRequest<Result<Guid>>;