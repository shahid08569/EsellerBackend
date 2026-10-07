using Eseller.Application.Common;
using MediatR;

namespace Eseller.Application.Features.PaymentShowcase.CreatePaymentLogo;

public record CreatePaymentLogoCommand(
    string Name,
    string ImageUrl,
    int SortOrder,
    bool IsActive) : IRequest<Result<Guid>>;
