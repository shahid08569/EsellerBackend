using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.AddPartner;

public record AddPartnerCommand(
    string Email,
    string Password,
    string? Permissions) : IRequest<Result<Guid>>;