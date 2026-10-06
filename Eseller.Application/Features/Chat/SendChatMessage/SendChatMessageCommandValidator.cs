using FluentValidation;

namespace Eseller.Application.Features.Chat.SendChatMessage;

public class SendChatMessageCommandValidator : AbstractValidator<SendChatMessageCommand>
{
    public SendChatMessageCommandValidator()
    {
        RuleFor(x => x.ConversationId)
            .NotEmpty().WithMessage("Conversation ID is required.");

        RuleFor(x => x)
            .Must(x => !string.IsNullOrWhiteSpace(x.Message) || !string.IsNullOrWhiteSpace(x.AttachmentUrl))
            .WithMessage("Message text or attachment is required.");

        RuleFor(x => x.Message)
            .MaximumLength(2000).WithMessage("Message cannot exceed 2000 characters.")
            .When(x => !string.IsNullOrEmpty(x.Message));

        RuleFor(x => x.AttachmentUrl)
            .MaximumLength(1000)
            .When(x => !string.IsNullOrEmpty(x.AttachmentUrl));

        RuleFor(x => x.AttachmentFileName)
            .MaximumLength(260)
            .When(x => !string.IsNullOrEmpty(x.AttachmentFileName));
    }
}
