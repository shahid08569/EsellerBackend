using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.Affiliate.RegisterAffiliate;

public record RegisterAffiliateCommand(
    string Name,
    string Email,
    string Password,
    decimal Latitude,
    decimal Longitude,
    string DeviceType,
    string? IpAddress = null,
    string? UserAgent = null) : IRequest<Result<Guid>>;