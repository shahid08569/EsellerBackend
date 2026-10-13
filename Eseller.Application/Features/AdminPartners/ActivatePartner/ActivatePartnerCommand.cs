using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.AdminPartners.ActivatePartner;

public record ActivatePartnerCommand(Guid PartnerId) : IRequest<Result>;
