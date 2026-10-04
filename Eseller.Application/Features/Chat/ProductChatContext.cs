namespace Eseller.Application.Features.Chat;

/// <summary>
/// Encodes / decodes product context stored on shop-support OrderRequest.OrderNotes.
/// Format: PRODUCT_CTX|{id}|{name}|{price}|{imageUrl}
/// </summary>
public static class ProductChatContext
{
    public const string Prefix = "PRODUCT_CTX|";

    public static string Encode(Guid productId, string name, decimal price, string? imageUrl)
    {
        var safeName = (name ?? "Product").Replace('|', '/');
        var safeImg = (imageUrl ?? string.Empty).Replace('|', '/');
        return $"{Prefix}{productId}|{safeName}|{price:0.##}|{safeImg}";
    }

    public static bool TryParse(
        string? notes,
        out Guid productId,
        out string name,
        out decimal price,
        out string? imageUrl)
    {
        productId = Guid.Empty;
        name = string.Empty;
        price = 0;
        imageUrl = null;

        if (string.IsNullOrWhiteSpace(notes) || !notes.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        var parts = notes.Split('|');
        if (parts.Length < 4)
            return false;

        if (!Guid.TryParse(parts[1], out productId))
            return false;

        name = parts[2];
        if (!decimal.TryParse(parts[3], out price))
            price = 0;
        imageUrl = parts.Length > 4 && !string.IsNullOrWhiteSpace(parts[4]) ? parts[4] : null;
        return true;
    }

    public static string BuildMessage(string name, decimal price, Guid productId, string? imageUrl)
    {
        var imgLine = string.IsNullOrWhiteSpace(imageUrl) ? "" : $"\nImage: {imageUrl}";
        return
            $"[PRODUCT INQUIRY]\n" +
            $"Product: {name}\n" +
            $"Price: Rs. {price:0.##}\n" +
            $"ProductId: {productId}{imgLine}\n" +
            $"Seller is chatting with Super Admin about this product.";
    }
}
