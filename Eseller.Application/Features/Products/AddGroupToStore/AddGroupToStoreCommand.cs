using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Products.AddGroupToStore;

public record AddGroupToStoreCommand(
    Guid ShopId,
    int Count,
    List<Guid>? CategoryIds = null,
    Guid? BrandId = null,
    decimal? MinPrice = null,
    decimal? MaxPrice = null,
    string? CustomBatchName = null
) : IRequest<Result<AddGroupToStoreResultDto>>;

public record AddGroupToStoreResultDto(
    Guid BatchId,
    string BatchName,
    int AddedCount,
    int TotalRequested,
    string Message
);
