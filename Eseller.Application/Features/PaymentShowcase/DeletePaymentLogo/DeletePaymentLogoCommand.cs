using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.DeletePaymentLogo;

public record DeletePaymentLogoCommand(Guid Id) : IRequest<Result>;
