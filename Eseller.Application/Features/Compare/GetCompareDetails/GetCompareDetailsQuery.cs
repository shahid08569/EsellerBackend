using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Compare.GetCompareDetails;

public record GetCompareDetailsQuery : IRequest<Result<CompareDetailsDto>>;