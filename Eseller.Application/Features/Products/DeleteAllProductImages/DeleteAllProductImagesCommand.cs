using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.DeleteAllProductImages;

public record DeleteAllProductImagesCommand(Guid ProductId) : IRequest<Result>;