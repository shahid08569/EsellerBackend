using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminCatalog.PurgeCatalog;

public record PurgeCatalogCommand(string Confirm) : IRequest<Result<PurgeCatalogResult>>;

public record PurgeCatalogResult(
    int ProductsDeleted,
    int BrandsDeleted,
    int CategoriesDeleted);
