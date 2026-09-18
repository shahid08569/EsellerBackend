using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.GetCategories;

public record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryTreeDto>>>;