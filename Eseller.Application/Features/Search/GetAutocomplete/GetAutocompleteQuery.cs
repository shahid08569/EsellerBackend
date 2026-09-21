using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Search.GetAutocomplete;

public record GetAutocompleteQuery(
    string Query,
    int Limit = 10) : IRequest<Result<List<AutocompleteDto>>>;