using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.GetPaymentLogos;

public record GetPaymentLogosQuery(bool? IsActive = null) : IRequest<Result<List<PaymentShowcaseLogoDto>>>;
