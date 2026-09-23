using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.DeactivatePartner;

public record DeactivatePartnerCommand(Guid PartnerId) : IRequest<Result>;