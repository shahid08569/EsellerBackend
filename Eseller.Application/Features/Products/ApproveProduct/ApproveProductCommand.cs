using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.ApproveProduct;

public record ApproveProductCommand(Guid ProductId) : IRequest<Result>;