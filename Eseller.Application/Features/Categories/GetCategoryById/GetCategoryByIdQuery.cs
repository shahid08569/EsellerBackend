using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.GetCategoryById;

public record GetCategoryByIdQuery(Guid CategoryId) : IRequest<Result<CategoryDto>>;