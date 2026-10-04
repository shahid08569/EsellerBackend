using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminCategories.UpdateCategoryHomepage;

public record UpdateCategoryHomepageCommand(
    Guid CategoryId,
    bool IsFeaturedOnHomepage,
    int HomepageDisplayOrder) : IRequest<Result>;