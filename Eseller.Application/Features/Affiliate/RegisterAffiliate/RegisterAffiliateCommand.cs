using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.RegisterAffiliate;

public record RegisterAffiliateCommand(
    string Name,
    string Email,
    string Password) : IRequest<Result<Guid>>;