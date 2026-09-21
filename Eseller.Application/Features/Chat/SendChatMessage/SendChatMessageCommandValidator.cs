using FluentValidation;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.OrderRequestId)
            .NotEmpty().WithMessage("Order request ID is required.");

        RuleFor(x => x.Message)
            .NotEmpty().WithMessage("Message is required.")
            .MaximumLength(2000).WithMessage("Message cannot exceed 2000 characters.");
    }
}