using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.SetProductImageCover;

public record SetProductImageCoverCommand(Guid ProductId, Guid ImageId) : IRequest<Result>;
