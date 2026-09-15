using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Engagement;

public class Wishlist : BaseEntity
{
    public Guid UserId { get; set; }
    public Guid ProductId { get; set; }
    public DateTime CreatedAt { get; set; }
}