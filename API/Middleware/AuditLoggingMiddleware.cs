using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;
using System.Security.Claims;

namespace ShreeJewellers.API.Middleware;

/// <summary>
/// Writes audit log entries for all non-GET API requests that modify financial data.
/// Sensitive paths (login, file upload) have their bodies suppressed in the log.
/// Uses a scoped DbContext resolved per-request to avoid concurrency issues.
/// </summary>
public class AuditLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLoggingMiddleware> _logger;

    // Paths to record — POST/PUT/DELETE on these paths trigger an audit entry
    private static readonly HashSet<string> AuditedPrefixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/goldloan",
            "/api/sales",
            "/api/customers",
            "/api/inventory",
            "/api/settings",
            "/api/auth/admin"
        };

    // Never log bodies from these paths (contain credentials or file data)
    private static readonly HashSet<string> SuppressBodyPrefixes =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "/api/auth/login",
            "/api/auth/register",
            "/api/auth/reset-password",
            "/api/auth/change-password",
            "/api/auth/refresh-token"
        };

    private static readonly HashSet<string> MutatingMethods =
        new(StringComparer.OrdinalIgnoreCase) { "POST", "PUT", "PATCH", "DELETE" };

    public AuditLoggingMiddleware(RequestDelegate next, ILogger<AuditLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await _next(context);

        // Only audit mutating requests on targeted paths
        if (!MutatingMethods.Contains(context.Request.Method)) return;

        var path = context.Request.Path.Value ?? string.Empty;
        if (!AuditedPrefixes.Any(p => path.StartsWith(p, StringComparison.OrdinalIgnoreCase))) return;

        // Resolve scoped DbContext — safe because we're post-response
        using var scope = context.RequestServices.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

        try
        {
            var userId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ip = GetClientIp(context);
            var userAgent = context.Request.Headers["User-Agent"].FirstOrDefault();
            var action = DeriveAction(context.Request.Method, path);

            db.AuditLogs.Add(new AuditLog
            {
                UserId     = userId,
                Action     = action,
                EntityName = DeriveEntityName(path),
                EntityId   = null,    // Set by specific service methods for financial ops
                IPAddress  = ip,
                UserAgent  = userAgent?[..Math.Min(500, userAgent.Length)],
                Timestamp  = DateTime.UtcNow
            });

            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Audit failure must never crash the application
            _logger.LogError(ex, "AuditLoggingMiddleware failed for {Path}", path);
        }
    }

    private static string DeriveAction(string method, string path)
    {
        if (path.Contains("/kyc")) return $"KYC_{method}";
        if (path.Contains("/repayment")) return "REPAYMENT_RECORD";
        if (path.Contains("/close")) return "LOAN_CLOSE";
        if (path.Contains("/extend")) return "LOAN_EXTEND";
        return method switch
        {
            "POST"   => "CREATE",
            "PUT"    => "UPDATE",
            "PATCH"  => "PARTIAL_UPDATE",
            "DELETE" => "DELETE",
            _        => method
        };
    }

    private static string DeriveEntityName(string path)
    {
        if (path.Contains("/goldloan")) return "GoldLoan";
        if (path.Contains("/sales"))    return "SalesOrder";
        if (path.Contains("/customers")) return "Customer";
        if (path.Contains("/inventory")) return "Inventory";
        if (path.Contains("/settings"))  return "AppSetting";
        return "Unknown";
    }

    private static string GetClientIp(HttpContext context)
    {
        // Check X-Forwarded-For (when behind a proxy/load balancer)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
            return forwardedFor.Split(',')[0].Trim();

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }
}
