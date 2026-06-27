using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ShreeJewellers.API.Middleware;

/// <summary>
/// Injects security HTTP response headers on every response.
/// Addresses OWASP A05: Security Misconfiguration.
/// </summary>
public class SecurityHeadersMiddleware
{
    private readonly RequestDelegate _next;
    private readonly string _allowedOrigins;
    private readonly IHostEnvironment _env;

    public SecurityHeadersMiddleware(RequestDelegate next, IConfiguration config, IHostEnvironment env)
    {
        _next = next;
        _allowedOrigins = config["Cors:AllowedOrigins"] ?? "https://shreejewellers.com";
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var headers = context.Response.Headers;

        // Prevent HTTPS downgrade attacks — 1-year max-age
        headers["Strict-Transport-Security"] =
            "max-age=31536000; includeSubDomains; preload";

        // Prevent MIME type sniffing
        headers["X-Content-Type-Options"] = "nosniff";

        // Prevent clickjacking (also handled by CSP frame-ancestors)
        headers["X-Frame-Options"] = "DENY";

        // Legacy XSS filter (modern browsers use CSP instead)
        headers["X-XSS-Protection"] = "1; mode=block";

        // Control referrer information
        headers["Referrer-Policy"] = "strict-origin-when-cross-origin";

        // Restrict browser APIs (no camera, microphone, geolocation needed)
        headers["Permissions-Policy"] =
            "camera=(), microphone=(), geolocation=(), payment=()";

        // Content Security Policy
        // - Production: strict (no inline scripts, connect only to self and configured CDNs)
        // - Development: relaxed to allow inline scripts and localhost connect for BrowserLink/dotnet-watch/Swagger UI
        if (_env.IsDevelopment())
        {
            headers["Content-Security-Policy"] =
                "default-src 'self' http: https: data: blob:; " +
                "script-src 'self' 'unsafe-inline' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "style-src 'self' https://cdn.jsdelivr.net https://fonts.googleapis.com 'unsafe-inline'; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data: blob: https:; " +
                // allow BrowserLink / dotnet-watch / local swagger connections in dev
                "connect-src 'self' http://localhost:52893 http://localhost:56655 https://localhost:56655 ws://localhost:52893 wss://localhost:44328 *; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "upgrade-insecure-requests;";
        }
        else
        {
            headers["Content-Security-Policy"] =
                "default-src 'self'; " +
                "script-src 'self' https://cdn.jsdelivr.net https://cdnjs.cloudflare.com; " +
                "style-src 'self' https://cdn.jsdelivr.net https://fonts.googleapis.com 'unsafe-inline'; " +
                "font-src 'self' https://fonts.gstatic.com; " +
                "img-src 'self' data: blob: https:; " +
                "connect-src 'self'; " +
                "frame-ancestors 'none'; " +
                "form-action 'self'; " +
                "upgrade-insecure-requests;";
        }

        // Remove server version disclosure headers
        headers.Remove("Server");
        headers.Remove("X-Powered-By");
        headers.Remove("X-AspNet-Version");
        headers.Remove("X-AspNetMvc-Version");

        await _next(context);
    }
}
