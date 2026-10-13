using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.UpdatePartner;

public record UpdatePartnerCommand(
    Guid PartnerId,
    string? Permissions,
    string? NewPassword = null) : IRequest<Result>;