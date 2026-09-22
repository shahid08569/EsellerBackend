using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Compare.AddToCompare;

public record AddToCompareCommand(Guid ProductId) : IRequest<Result<Guid>>;