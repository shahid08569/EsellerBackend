using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Search.GetSuggestions;

public record GetSuggestionsQuery(
    string Query) : IRequest<Result<SearchSuggestionsDto>>;

public record SearchSuggestionsDto(
    List<AutocompleteDto> Products,
    List<AutocompleteDto> Categories,
    List<AutocompleteDto> Brands);