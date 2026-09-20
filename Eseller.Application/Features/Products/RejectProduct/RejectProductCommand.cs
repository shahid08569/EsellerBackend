using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.RejectProduct;

public record RejectProductCommand(
    Guid ProductId,
    string Reason) : IRequest<Result>;