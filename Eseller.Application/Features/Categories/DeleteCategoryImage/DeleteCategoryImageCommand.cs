using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Categories.DeleteCategoryImage;

public record DeleteCategoryImageCommand(Guid CategoryId) : IRequest<Result>;