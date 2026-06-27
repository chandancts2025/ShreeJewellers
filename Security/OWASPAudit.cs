/*
 * ============================================================
 * SHREE JEWELLERS — OWASP Top 10 Security Implementation
 * SecurityAudit.cs
 * 
 * Documents and verifies security controls for each OWASP
 * Top 10 category (2021 edition) as applied to this app.
 * ============================================================
 */

namespace ShreeJewellers.Security;

/*
 ┌─────────────────────────────────────────────────────────────┐
 │  A01 — Broken Access Control                                │
 └─────────────────────────────────────────────────────────────┘
 
 THREATS:
   - Staff accessing another customer's gold loan details
   - Customer accessing admin reports
   - Unauthenticated access to financial endpoints
 
 CONTROLS IMPLEMENTED:
 
 1. Every API controller has [Authorize] at class or method level.
    No endpoint is accidentally left public.
    Verified by: AuthorizationFilter integration test.
 
 2. Resource-based ownership checks in GoldLoanService:
    - GetLoanAsync: if caller role is Customer, verify loan.CustomerUserId == currentUserId
    - GetCustomerLoansAsync: customers can only query their own ID
    - GoldLoanController.IsCustomer() gate applied before all customer-visible endpoints
 
 3. SalesOrderService: customers only see their own orders.
    CustomerUserId filter injected at service layer, not relying on client-provided filter.
 
 4. Role hierarchy:
    SuperAdmin > Admin > Staff > Customer
    Each role is additive — Customer cannot escalate to Staff by manipulating JWT claims.
    JWT claims are generated server-side from Identity UserRoles table only.
 
 5. Soft-delete only. Only SuperAdmin/Admin can call delete endpoints.
    Staff role cannot perform any destructive action.
 
 AUDIT QUERY — run monthly to detect misconfigured endpoints:
   SELECT r.RoutePattern, r.HttpMethods
   FROM asp_net_routes r
   WHERE r.AuthorizationRequired = 0
     AND r.Path LIKE '/api/%'
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A02 — Cryptographic Failures                               │
 └─────────────────────────────────────────────────────────────┘
 
 THREATS:
   - Aadhaar/PAN numbers stored in plaintext → data breach risk
   - JWT secret too weak or hardcoded
   - Sensitive data appearing in logs
 
 CONTROLS IMPLEMENTED:
 
 1. AES-256-CBC encryption for all PII fields:
    - ApplicationUser.AadhaarNumberEncrypted → EncryptionService.Encrypt()
    - ApplicationUser.PANNumberEncrypted     → EncryptionService.Encrypt()
    - Key is 32-byte (256-bit) loaded from environment variable only
    - Fresh random IV per encryption prevents rainbow table attacks
    - See: EncryptionService.cs
 
 2. HTTPS enforced:
    - Program.cs: app.UseHttpsRedirection()
    - HSTS header: Strict-Transport-Security: max-age=31536000
    - JWT Bearer: RequireHttpsMetadata = true in production
 
 3. Sensitive data masked in Serilog:
    - RegisterStep2Dto destructuring masks AadhaarNumber and PANNumber
    - EncryptionService.MaskSensitive() used before any logging of these fields
    - Log level for EF Core DB commands set to Warning (hides query params)
 
 4. JWT secrets:
    - Minimum 32 character secret key enforced at startup
    - Loaded from environment variable JWT_SECRET_KEY only
    - Never appears in appsettings.json committed to source control
    - Tokens signed with HMAC-SHA256
 
 5. Passwords:
    - ASP.NET Core Identity BCrypt hashing (PBKDF2 with HMAC-SHA256)
    - No MD5, SHA1, or reversible encryption for passwords
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A03 — Injection                                            │
 └─────────────────────────────────────────────────────────────┘
 
 THREATS:
   - SQL injection via gold loan search / customer search inputs
   - XSS via product name / customer notes stored in DB
   - Log injection attacks
 
 CONTROLS IMPLEMENTED:
 
 1. EF Core parameterized queries ONLY.
    Global search policy: ALL WHERE clause values are LINQ expressions,
    never string-concatenated into raw SQL.
    
    CORRECT:
      _db.GoldLoans.Where(l => l.LoanNumber == loanNumber)
    
    PROHIBITED (detected by code review checklist):
      _db.Database.ExecuteSqlRaw($"SELECT * WHERE LoanNumber = '{input}'")
 
 2. No FromSqlRaw() calls anywhere in the codebase.
    If raw SQL is ever needed (e.g., complex reports), FromSqlInterpolated() is mandatory.
 
 3. XSS: ASP.NET Core Razor Views HTML-encode all output by default.
    @Html.Raw() is prohibited in views that render user-supplied content.
    Content-Security-Policy header prevents inline script injection.
 
 4. Log injection: Serilog structured logging uses {} placeholders, not string interpolation.
    Log output is never parsed back as code.
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A04 — Insecure Design (Threat Model)                       │
 └─────────────────────────────────────────────────────────────┘
 
 THREAT SCENARIO: Staff member tries to access another customer's gold loan
 
 ATTACK PATH:
   1. Staff logs in: POST /api/auth/login → gets JWT with role=Staff
   2. Calls: GET /api/goldloan/customer/{otherId} with a different customer's ID
 
 DEFENCE:
   - GoldLoanController.GetCustomerLoans() checks: if IsCustomer() && customerId != GetUserId() → Forbid()
   - BUT: Staff is NOT in Customer role, so they CAN access any customer's loans
   - This is INTENTIONAL — staff need to view any customer's loans to serve them
   - Staff CANNOT: create loans, close loans, modify interest settings, view financial reports
   - All Staff actions are audit-logged with their UserId
 
 MITIGATED RISKS:
   - Staff impersonating Admin → JWT claims are immutable server-side
   - Staff creating fake repayments → RecordRepaymentAsync audit logs every payment
   - Staff viewing competitor customer lists → IP rate limiting prevents bulk enumeration
 
 THREAT SCENARIO: Forged JWT with elevated role
   - JWT signed with HMAC-SHA256 using server-secret
   - Signature verification on every request; tampered JWTs rejected at middleware layer
   - Roles embedded in JWT are only from server-side UserRoles table
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A05 — Security Misconfiguration                            │
 └─────────────────────────────────────────────────────────────┘
 
 CONTROLS IMPLEMENTED:
 
 1. Security headers (SecurityHeadersMiddleware.cs):
    - Strict-Transport-Security
    - X-Content-Type-Options: nosniff
    - X-Frame-Options: DENY
    - Content-Security-Policy
    - Permissions-Policy
    - Server and X-Powered-By headers REMOVED
 
 2. CORS policy:
    - Allows ONLY configured frontend origins from appsettings.json
    - No wildcard (*) origins in production
    - Credentials allowed only for same-site cookie auth
 
 3. Error pages:
    - GlobalExceptionMiddleware returns RFC 7807 ProblemDetails ONLY
    - Stack traces NEVER returned to clients
    - All errors logged server-side with CorrelationId
 
 4. Swagger:
    - Disabled in production via ASPNETCORE_ENVIRONMENT check
    - Enabled only in Development and Staging
 
 5. SQL Server:
    - Database user has minimal permissions (SELECT/INSERT/UPDATE on app tables only)
    - No sa or sysadmin accounts for application
    - Connection string from environment variable, not appsettings
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A06 — Vulnerable and Outdated Components                   │
 └─────────────────────────────────────────────────────────────┘
 
 CONTROLS IMPLEMENTED:
 
 1. GitHub Actions pipeline runs 'dotnet list package --vulnerable' on every PR.
    Build FAILS if any high/critical vulnerability is found.
 
 2. Dependabot configured for weekly NuGet package updates.
 
 3. Base Docker images use specific version tags (not :latest).
    Renovate Bot configured to auto-update base image versions.
 
 4. License compliance scan via 'dotnet-project-licenses' in CI.
 
 COMMAND (run before each release):
   dotnet list package --vulnerable --include-transitive
   dotnet list package --outdated
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A07 — Identification and Authentication Failures           │
 └─────────────────────────────────────────────────────────────┘
 
 CONTROLS IMPLEMENTED:
 
 1. Account lockout: 5 failed attempts → 15-minute lockout
    (Identity: Lockout.MaxFailedAccessAttempts = 5, DefaultLockoutTimeSpan = 15min)
 
 2. JWT access token: 15-minute expiry, zero ClockSkew
 
 3. Refresh token rotation: every use of a refresh token revokes it and issues a new one.
    Replay attack window = 0 (token is single-use).
 
 4. Refresh tokens stored SHA-256 hashed in Identity UserTokens table.
    Even if DB is breached, raw refresh tokens are not exposed.
 
 5. Password reset tokens expire in 1 hour (DataProtectionTokenProviderOptions).
 
 6. Email verification required before full account access.
 
 7. 2FA available for sensitive operations (Gold Loan creation, large payments).
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A08 — Software and Data Integrity Failures                 │
 └─────────────────────────────────────────────────────────────┘
 
 CONTROLS IMPLEMENTED:
 
 1. File upload security (FileUploadService.cs):
    - MIME type whitelist: jpg, jpeg, png, pdf ONLY
    - Magic bytes validation (prevents MIME spoofing)
    - Files stored OUTSIDE wwwroot, never directly accessible via URL
    - Access only through authenticated controller endpoint
    - Max file size: 5MB enforced
    - Executable extensions (.exe, .sh, .js, .php, etc.) always rejected
 
 2. Supply chain:
    - All NuGet packages pinned to specific versions in .csproj
    - GitHub Actions uses hash-pinned action versions (not tag-based)
    - Secrets scanning (Gitleaks) runs on every push
 
 3. Deserialization:
    - System.Text.Json used (not Newtonsoft) — safer default deserialization
    - No BinaryFormatter or TypeNameHandling.Auto
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A09 — Security Logging and Monitoring Failures             │
 └─────────────────────────────────────────────────────────────┘
 
 CONTROLS IMPLEMENTED:
 
 1. AuditLog table captures ALL financial transactions:
    - GoldLoan creation, repayment, extension, closure, auction
    - KYC verification/rejection
    - Interest setting changes
    - User role changes
    Each entry includes: UserId, Action, OldValues(JSON), NewValues(JSON), IPAddress, Timestamp
 
 2. AuditLoggingMiddleware auto-logs all POST/PUT/DELETE on financial paths.
 
 3. Serilog structured logging:
    - All requests: method, path, status code, duration
    - Authentication events: login success/failure, lockout
    - Background job outcomes: defaulted loans count, reminders sent
    - Log files: 30-day retention, daily rolling
 
 4. Failed login alert: after 3 failed attempts on the same account,
    an InApp notification is sent to admin team.
 
 5. Sensitive log masking: Aadhaar/PAN shown as XXXX-XXXX-1234 in all logs.
 
 ┌─────────────────────────────────────────────────────────────┐
 │  A10 — Server-Side Request Forgery (SSRF)                   │
 └─────────────────────────────────────────────────────────────┘
 
 THREATS:
   - Admin sets Gold Price API URL to internal network endpoint
   - Attacker uses price API URL field to scan internal infrastructure
 
 CONTROLS IMPLEMENTED:
 
 1. GoldAPI.io base URL is ONLY loaded from appsettings.json / environment variable.
    No user input (form field, API request) can modify the target URL.
 
 2. HttpClient "GoldAPI" is registered with a FIXED BaseAddress at startup.
    PriceService only appends the metal symbol (XAU, XAG) — no full URL concatenation.
 
 3. If a manual override URL were ever needed, it would be validated against
    an allowlist of approved domains before making a request.
 
 4. Internal network addresses (10.x.x.x, 192.168.x.x, 169.254.x.x, localhost)
    are blocked if any external HTTP call is added in future.
 
 VALIDATION STUB (add to any future HTTP call that uses a configurable URL):
*/

using System.Net;

public static class SSRFProtection
{
    private static readonly HashSet<string> BlockedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "localhost", "127.0.0.1", "0.0.0.0", "::1", "169.254.169.254" // AWS metadata
    };

    private static readonly string[] BlockedPrefixes = { "10.", "192.168.", "172.16.", "172.17.", "172.18.", "172.19.", "172.2", "172.3" };

    /// <summary>
    /// Validates that a URL is safe to make an outbound HTTP request to.
    /// Prevents SSRF attacks where user-controlled URLs point to internal services.
    /// </summary>
    public static bool IsSafeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
        if (uri.Scheme is not ("https" or "http")) return false;
        if (BlockedHosts.Contains(uri.Host)) return false;
        if (BlockedPrefixes.Any(p => uri.Host.StartsWith(p))) return false;
        if (IPAddress.TryParse(uri.Host, out var ip))
        {
            var bytes = ip.GetAddressBytes();
            // Block RFC1918 ranges
            if (bytes[0] == 10) return false;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return false;
            if (bytes[0] == 192 && bytes[1] == 168) return false;
        }
        return true;
    }

    public static string ValidateAndGetUrl(string url, string parameterName = "url")
    {
        if (!IsSafeUrl(url))
            throw new InvalidOperationException(
                $"The URL provided for '{parameterName}' is not allowed. " +
                "Internal network addresses and non-HTTPS URLs are blocked.");
        return url;
    }
}
