using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Compare.ClearCompare;

public record ClearCompareCommand : IRequest<Result>;