using Eseller.Domain.Common;

namespace Eseller.Domain.Entities.Catalog;

public class Shop : AuditableEntity, ISoftDelete
{
    public Guid ShopkeeperId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? LogoUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? Phone { get; set; }
    public string? Address { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public decimal Rating { get; set; }
    public int TotalProducts { get; set; }
    public Guid? ShopCategoryId { get; set; }
    public string? CustomBadgeText { get; set; }
    public Guid? PartnerId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime? DeletedAt { get; set; }
}