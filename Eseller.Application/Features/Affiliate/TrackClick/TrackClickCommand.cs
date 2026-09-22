using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.TrackClick;

public record TrackClickCommand(
    string ReferralCode,
    Guid? ProductId,
    string? IpAddress,
    string? UserAgent) : IRequest<Result<Guid>>;