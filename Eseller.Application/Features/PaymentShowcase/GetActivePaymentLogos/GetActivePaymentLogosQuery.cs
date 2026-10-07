using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.GetActivePaymentLogos;

public record GetActivePaymentLogosQuery() : IRequest<Result<List<PaymentShowcaseLogoDto>>>;
