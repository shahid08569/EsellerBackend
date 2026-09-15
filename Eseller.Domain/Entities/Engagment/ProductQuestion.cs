using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Engagement;

public class ProductQuestion : BaseEntity
{
    public Guid ProductId { get; set; }
    public Guid UserId { get; set; }
    public string Question { get; set; } = string.Empty;
    public string? Answer { get; set; }
    public Guid? AnsweredByShopkeeperId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? AnsweredAt { get; set; }
}