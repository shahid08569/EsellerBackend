namespace Eseller.Infrastructure.Persistence;

/// <summary>
/// Default public media for seeded categories / brands (homepage logos).
/// </summary>
internal static class CatalogMedia
{
    private static readonly Dictionary<string, string> CategoryImages = new(StringComparer.OrdinalIgnoreCase)
    {
        ["electronics"] = "https://images.unsplash.com/photo-1505740420928-5e560c06d30e?auto=format&fit=crop&w=400&h=400&q=80",
        ["mobiles-tablets"] = "https://images.unsplash.com/photo-1511707171634-5f897ff02aa9?auto=format&fit=crop&w=400&h=400&q=80",
        ["computers-laptops"] = "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=400&h=400&q=80",
        ["fashion"] = "https://images.unsplash.com/photo-1521572163474-6864f9cf17ab?auto=format&fit=crop&w=400&h=400&q=80",
        ["home-living"] = "https://images.unsplash.com/photo-1555041469-a586c61ea9bc?auto=format&fit=crop&w=400&h=400&q=80",
        ["beauty-personal-care"] = "https://images.unsplash.com/photo-1596462502278-27bfdc403348?auto=format&fit=crop&w=400&h=400&q=80",
        ["sports-outdoors"] = "https://images.unsplash.com/photo-1517836357463-d25dfeac3438?auto=format&fit=crop&w=400&h=400&q=80",
        ["groceries"] = "https://images.unsplash.com/photo-1542838132-92c53300491e?auto=format&fit=crop&w=400&h=400&q=80",
        ["toys-kids"] = "https://images.unsplash.com/photo-1558060370-d644479cb6f7?auto=format&fit=crop&w=400&h=400&q=80",
        ["automotive"] = "https://images.unsplash.com/photo-1492144534655-ae79c964c9d7?auto=format&fit=crop&w=400&h=400&q=80"
    };

    private static readonly Dictionary<string, string> BrandLogos = new(StringComparer.OrdinalIgnoreCase)
    {
        ["apple"] = "https://cdn.simpleicons.org/apple/111111",
        ["samsung"] = "https://cdn.simpleicons.org/samsung/1428A0",
        ["xiaomi"] = "https://cdn.simpleicons.org/xiaomi/FF6900",
        ["sony"] = "https://cdn.simpleicons.org/sony/000000",
        ["huawei"] = "https://cdn.simpleicons.org/huawei/CF0A2C",
        ["nike"] = "https://cdn.simpleicons.org/nike/111111",
        ["adidas"] = "https://cdn.simpleicons.org/adidas/000000",
        ["generic-unbranded"] = "https://cdn.simpleicons.org/shopify/96BF48"
    };

    public static string? CategoryImage(string slug) =>
        CategoryImages.TryGetValue(slug, out var url) ? url : null;

    public static string? BrandLogo(string slug) =>
        BrandLogos.TryGetValue(slug, out var url) ? url : null;
}
