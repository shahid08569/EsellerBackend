namespace Eseller.Application.Features.ProductQuestions;

public record ProductQuestionDto(
    Guid Id,
    Guid ProductId,
    Guid UserId,
    string UserName,
    string Question,
    bool IsApproved,
    DateTime CreatedAt,
    string? Answer,
    Guid? AnsweredByShopkeeperId,
    string? AnsweredByName,
    DateTime? AnsweredAt);