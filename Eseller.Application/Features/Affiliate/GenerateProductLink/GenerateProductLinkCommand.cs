using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.GenerateProductLink;

public record GenerateProductLinkCommand(
    Guid ProductId) : IRequest<Result<ReferralLinkDto>>;