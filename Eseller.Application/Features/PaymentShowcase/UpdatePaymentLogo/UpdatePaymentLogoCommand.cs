using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.UpdatePaymentLogo;

public record UpdatePaymentLogoCommand(
    Guid Id,
    string Name,
    string ImageUrl,
    int SortOrder,
    bool IsActive) : IRequest<Result>;
