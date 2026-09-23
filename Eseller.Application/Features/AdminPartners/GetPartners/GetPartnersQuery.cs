using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.GetPartners;

public record GetPartnersQuery(
    int PageNumber = 1,
    int PageSize = 20) : IRequest<Result<PagedList<PartnerDto>>>;