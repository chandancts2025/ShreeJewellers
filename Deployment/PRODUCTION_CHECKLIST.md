# ============================================================
# SHREE JEWELLERS — Production Readiness Checklist
# 50 items across 7 categories
# Review before every major deployment
# ============================================================

## CATEGORY 1: SECURITY (15 items)

### Authentication & Authorization
- [ ] 1.  JWT_SECRET_KEY is ≥32 chars and stored only in environment variable / secret manager
- [ ] 2.  Refresh token rotation is working (test with integration test AuthControllerTests)
- [ ] 3.  Account lockout triggers after 5 failed attempts (test with login-lockout test)
- [ ] 4.  All admin endpoints require [Authorize(Roles = "Admin,SuperAdmin")] - no gaps
- [ ] 5.  Customer endpoints have resource-based ownership checks (own loans/orders only)

### Data Protection
- [ ] 6.  Aadhaar / PAN numbers are AES-256 encrypted in DB (verify with SELECT on Users table)
- [ ] 7.  AES_ENCRYPTION_KEY is 32 bytes base64-encoded (verify with: echo -n $AES_ENCRYPTION_KEY | base64 -d | wc -c)
- [ ] 8.  No sensitive PII appears in application logs (review logs/shreejewellers-*.log)
- [ ] 9.  Uploaded files stored outside wwwroot and served via authenticated endpoint only
- [ ] 10. File uploads reject executables (.exe, .sh, .php) via magic bytes check

### Network & Headers
- [ ] 11. HTTPS enforced in production (RequireHttpsMetadata=true in JwtBearer settings)
- [ ] 12. HSTS header present: Strict-Transport-Security: max-age=31536000
- [ ] 13. Security headers middleware verified (curl -I https://yourdomain.com | grep -i "x-frame")
- [ ] 14. CORS only allows configured origins (no wildcard *)
- [ ] 15. Swagger UI disabled in production (check ASPNETCORE_ENVIRONMENT=Production)

---

## CATEGORY 2: DATABASE (8 items)

- [ ] 16. EF Core migrations applied (dotnet ef database update runs cleanly)
- [ ] 17. Default SuperAdmin account password changed from Admin@123! to strong unique password
- [ ] 18. SQL Server runs as non-sa user with minimum required permissions
- [ ] 19. Connection string uses environment variable, not appsettings.json
- [ ] 20. Database backup job scheduled (usp_DailyBackup runs Sunday full + Mon-Sat diff)
- [ ] 21. Backup restore tested (last full backup restored successfully in test environment)
- [ ] 22. Index maintenance job scheduled (usp_MaintainIndexes runs weekly)
- [ ] 23. SQL Server firewall: port 1433 not exposed to internet (only internal network/Docker)

---

## CATEGORY 3: APPLICATION (10 items)

- [ ] 24. All xUnit tests pass: dotnet test shows 0 failures
- [ ] 25. NuGet vulnerability audit clean: dotnet list package --vulnerable shows no critical/high
- [ ] 26. Health endpoints respond: /health/live (200), /health/ready (200), /health/db (200)
- [ ] 27. Gold price API working: GET /api/prices/current returns valid rates
- [ ] 28. Background job (LoanStatusCheckerService) starts and runs: check logs for "LoanStatusChecker: job completed"
- [ ] 29. Email delivery tested: send test email via /api/auth/forgot-password
- [ ] 30. Invoice PDF generation tested: create a sale, download invoice via /api/sales/{id}/invoice
- [ ] 31. KYC flow tested end-to-end: Register → KYC submit → Admin verify → Customer accesses loan features
- [ ] 32. Gold loan lifecycle tested: Create → Repay partial → Repay full → Closed status + gold return notification
- [ ] 33. Interest calculation verified: Compare manual calculation for ₹50,000 at 2%/month for 3 months = ₹3,000

---

## CATEGORY 4: INFRASTRUCTURE (7 items)

- [ ] 34. Docker containers healthy: docker ps shows all services as "healthy"
- [ ] 35. Nginx config tested: nginx -t passes without errors
- [ ] 36. SSL certificate valid and auto-renewing (Let's Encrypt / purchased cert)
- [ ] 37. Container restart policy set to "unless-stopped" in docker-compose.yml
- [ ] 38. Volume mounts verified: uploads and logs directories exist and writable
- [ ] 39. Container runs as non-root user (appuser, uid 1001) — not as root
- [ ] 40. Resource limits set: API ≤512MB RAM, Web ≤256MB RAM, SQL ≤2GB RAM

---

## CATEGORY 5: MONITORING & LOGGING (5 items)

- [ ] 41. Serilog writing to /app/logs directory (verify file exists and is growing)
- [ ] 42. AuditLogs table receiving entries (check after test gold loan creation)
- [ ] 43. Failed login attempts logged (attempt 3 consecutive failed logins, check logs)
- [ ] 44. Low-stock alert tested (adjust a product stock below reorder level, check notifications table)
- [ ] 45. Application crash alert configured (Docker health check → alert if container restarts 3x)

---

## CATEGORY 6: CONTENT & CONFIG (5 items)

- [ ] 46. Shop name, address, GST number updated in AppSettings table (Settings page → Shop Details)
- [ ] 47. Default interest rate configured (Settings → Interest Rates shows active rate)
- [ ] 48. WhatsApp FAB URL updated with real shop WhatsApp number in _Layout.cshtml
- [ ] 49. Email templates customized with correct shop branding
- [ ] 50. Gold Price API key is valid and tested (GET /api/prices/current returns non-zero rates)

---

## ENVIRONMENT VARIABLES REFERENCE

Copy this to your .env file or set in server/Docker secrets:

```bash
# ── Database ─────────────────────────────────────────────────
SQL_SA_PASSWORD=YourStrongP@ssword123!        # Min 8 chars, upper+lower+number+special
# Full connection string for the API
ConnectionStrings__DefaultConnection="Server=sqlserver,1433;Database=ShreeJewellers;User=sa;Password=YourStrongP@ssword123!;TrustServerCertificate=True"

# ── JWT Authentication ────────────────────────────────────────
# Generate: openssl rand -base64 48
JWT_SECRET_KEY=your-super-secret-jwt-key-minimum-32-characters-long-random

# ── AES Encryption (Aadhaar/PAN) ─────────────────────────────
# Generate: openssl rand -base64 32    (output will be 44 chars)
AES_ENCRYPTION_KEY=your-aes-256-key-base64-encoded-32-bytes-exact

# ── Gold Price API ────────────────────────────────────────────
# Register at: https://www.goldapi.io
GOLD_API_KEY=goldapi-XXXXXXXXXXXXXXXX-io

# ── Email (SMTP) ──────────────────────────────────────────────
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_SENDER_EMAIL=no-reply@shreejewellers.com
SMTP_SENDER_PASSWORD=your-gmail-app-password  # Use Gmail App Password, not account password

# ── Twilio SMS ────────────────────────────────────────────────
TWILIO_ACCOUNT_SID=ACxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx
TWILIO_AUTH_TOKEN=your_twilio_auth_token
TWILIO_FROM_NUMBER=+91XXXXXXXXXX

# ── Application ───────────────────────────────────────────────
ASPNETCORE_ENVIRONMENT=Production
WEB_ORIGIN=https://shreejewellers.com
IMAGE_TAG=latest

# ── Optional: WhatsApp Business (Future Feature) ─────────────
# WHATSAPP_ACCESS_TOKEN=your_meta_whatsapp_token
# WHATSAPP_PHONE_NUMBER_ID=your_phone_number_id

# ── Optional: MSG91 SMS (Future Feature) ─────────────────────
# MSG91_AUTH_KEY=your_msg91_auth_key
# MSG91_SENDER_ID=SRJWL

# ── Optional: Razorpay Bank Transfers (Future Feature) ────────
# BANK_API_KEY=rzp_live_xxxxxxxxxxxxx
# BANK_API_SECRET=your_razorpay_secret
```

---

## APPSETTINGS.PRODUCTION.JSON STRUCTURE

(Placeholder values — real values come from environment variables above)

```json
{
  "ConnectionStrings": {
    "DefaultConnection": ""
  },
  "JwtSettings": {
    "SecretKey":                  "",
    "Issuer":                     "ShreeJewellers.API",
    "Audience":                   "ShreeJewellers.Web",
    "AccessTokenExpiryMinutes":   "15",
    "RefreshTokenExpiryDays":     "7",
    "RememberMeRefreshTokenDays": "30"
  },
  "GoldPriceApi": {
    "ApiKey":                  "",
    "BaseUrl":                 "https://www.goldapi.io/api/",
    "RefreshIntervalMinutes":  "15",
    "Currency":                "INR"
  },
  "EmailSettings": {
    "SmtpHost":        "",
    "SmtpPort":        "587",
    "SenderEmail":     "",
    "SenderPassword":  "",
    "SenderName":      "Shree Jewellers"
  },
  "TwilioSettings": {
    "AccountSid":  "",
    "AuthToken":   "",
    "FromNumber":  ""
  },
  "EncryptionSettings": {
    "AESKey": ""
  },
  "FileStorage": {
    "BasePath":          "/app/secure-uploads",
    "MaxFileSizeMB":     "5",
    "AllowedExtensions": ".jpg,.jpeg,.png,.pdf"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://shreejewellers.com",
      "https://www.shreejewellers.com"
    ]
  },
  "Logging": {
    "LogLevel": {
      "Default":                        "Warning",
      "Microsoft.AspNetCore":           "Warning",
      "ShreeJewellers":                 "Information"
    }
  },
  "Serilog": {
    "MinimumLevel": {
      "Default":   "Information",
      "Override": {
        "Microsoft":                     "Warning",
        "Microsoft.EntityFrameworkCore": "Warning"
      }
    }
  }
}
```
