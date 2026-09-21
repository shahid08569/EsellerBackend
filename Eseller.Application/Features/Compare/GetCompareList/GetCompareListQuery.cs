using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Compare.GetCompareList;

public record GetCompareListQuery : IRequest<Result<IReadOnlyList<CompareItemDto>>>;