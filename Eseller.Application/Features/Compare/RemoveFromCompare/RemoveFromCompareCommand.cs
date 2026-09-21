using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Compare.RemoveFromCompare;

public record RemoveFromCompareCommand(Guid ProductId) : IRequest<Result>;