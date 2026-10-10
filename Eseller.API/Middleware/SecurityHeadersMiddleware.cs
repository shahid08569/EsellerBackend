namespace Eseller.API.Middleware;

/// <summary>
/// Enforces standard OWASP HTTP security headers on all HTTP responses.
/// Protects against XSS, clickjacking, MIME sniffing, and information disclosure.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;

    public SecurityHeadersMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            // 1. Prevent MIME-sniffing
            headers.TryAdd("X-Content-Type-Options", "nosniff");

            // 2. Prevent clickjacking (allow framing only by same origin)
            headers.TryAdd("X-Frame-Options", "SAMEORIGIN");

            // 3. Cross-Site Scripting filter (standard legacy browser protection)
            headers.TryAdd("X-XSS-Protection", "1; mode=block");

            // 4. Referrer Policy: Send full referrer to same-origin, only origin on cross-origin HTTPS
            headers.TryAdd("Referrer-Policy", "strict-origin-when-cross-origin");

            // 5. Restrict dangerous browser features by default
            headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=(self)");

            // 6. Remove Server / X-Powered-By leakage
            headers.Remove("Server");
            headers.Remove("X-Powered-By");

            return Task.CompletedTask;
        });

        await _next(context);
    }
}
