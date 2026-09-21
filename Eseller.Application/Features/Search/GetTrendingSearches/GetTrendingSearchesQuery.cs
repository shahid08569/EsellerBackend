using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Search.GetTrendingSearches;

public record GetTrendingSearchesQuery(
    int Limit = 10) : IRequest<Result<List<string>>>;