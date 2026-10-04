using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Homepage.GetHomepageCategories;

public record GetHomepageCategoriesQuery(
    int Limit = 11,
    int ProductsPerCategory = 8) : IRequest<Result<List<HomepageCategorySectionDto>>>;