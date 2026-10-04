using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.UpdateCategory;

public record UpdateCategoryCommand(
    Guid CategoryId,
    string Name,
    string? SeoTitle,
    string? SeoDescription,
    bool? IsActive = null) : IRequest<Result>;