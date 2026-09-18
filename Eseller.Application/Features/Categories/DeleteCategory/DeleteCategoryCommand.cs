using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.DeleteCategory;

public record DeleteCategoryCommand(Guid CategoryId) : IRequest<Result>;