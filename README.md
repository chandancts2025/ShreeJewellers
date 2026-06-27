# 💍 Shree Jewellers — Full-Stack .NET 8 Web Application

A complete, production-ready Gold & Silver Jewellery Shop Management System with Gold Loans, Inventory, Sales, Reports, and a luxury web frontend.

---

## 🏗️ Architecture Overview

```
ShreeJewellers/
├── Domain/                    ← Entities, Enums, Interfaces (no dependencies)
├── Application/               ← DTOs, Validators, Mappings (depends on Domain)
├── Infrastructure/            ← EF Core, Services, Repositories (depends on Application)
│   ├── Data/                  ← DbContext, Configurations, Migrations
│   ├── Services/              ← Business logic (GoldLoan, JWT, KYC, etc.)
│   ├── Repositories/          ← Data access layer
│   └── BackgroundJobs/        ← LoanStatusCheckerService
├── API/                       ← ASP.NET Core Web API (Controllers, Middleware)
├── Web/                       ← ASP.NET Core MVC Frontend (Razor Views)
├── Tests/
│   ├── UnitTests/             ← xUnit + Moq tests
│   └── IntegrationTests/      ← WebApplicationFactory tests
├── Security/                  ← OWASP audit documentation
├── Deployment/
│   ├── Docker/                ← Dockerfiles + docker-compose
│   ├── GitHub/                ← CI/CD pipeline
│   └── SQL/                   ← Maintenance scripts
└── Features/                  ← Future feature stubs
```

---

## 🚀 Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server 2019+ (or Docker)
- Visual Studio 2022 / VS Code / Rider

### 1. Clone and restore
```bash
git clone https://github.com/yourorg/shreejewellers.git
cd shreejewellers
dotnet restore ShreeJewellers.sln
```

### 2. Set environment variables
```bash
# Minimum required for development
export JWT_SECRET_KEY="dev-secret-key-minimum-32-characters-long"
export AES_ENCRYPTION_KEY="$(openssl rand -base64 32)"
# Copy appsettings.json and fill in your SQL Server connection
```

### 3. Apply database migrations
```bash
cd API
dotnet ef database update
```

### 4. Run the API
```bash
cd API
dotnet run
# API: https://localhost:5001
# Swagger: https://localhost:5001/swagger
```

### 5. Run the Web frontend
```bash
cd Web
dotnet run
# Web: https://localhost:5002
```

### 6. Default login
- Email: `superadmin@shreejewellers.com`
- Password: `Admin@123!`  ← **Change immediately in production**

---

## 🐳 Docker Compose (Recommended)

```bash
# Create .env from template
cp Deployment/Docker/.env.example .env
# Edit .env with your secrets

cd Deployment/Docker
docker-compose up -d

# Services:
# https://localhost       → Nginx (public site)
# http://localhost:8080   → API (internal)
# http://localhost:8081   → Web MVC (internal)
# localhost:1433          → SQL Server (local only)
```

---

## 📋 Project Modules

### Core Modules
| Module | Files | Description |
|---|---|---|
| **Domain** | 12 entities, 8 enums | ApplicationUser, GoldLoan, Product, SalesOrder, etc. |
| **Auth & KYC** | AuthController, JwtTokenService, KYCService | JWT + refresh tokens, 3-step KYC, AES-256 PII |
| **Gold Loans** | GoldLoanService, InterestCalculationEngine | Create, repay, interest (daily/monthly/yearly), defaults |
| **Inventory & Sales** | ProductService, SalesOrderService, InvoiceGeneratorService | GST billing, PDF invoices, barcode QR |
| **Prices** | PriceService | Live gold/silver from GoldAPI.io, 15-min cache, manual override |
| **Reports** | ReportService, ExcelExportService | 18 reports, Excel/PDF export |
| **Dashboard** | DashboardService | Real-time KPIs, Chart.js data |
| **Frontend** | 18 Razor Views | Luxury jewelry theme, public site + customer portal + admin |

### Key Business Logic
- **Interest Calculation**: `InterestCalculationEngine` — simple/compound, daily/monthly/yearly, penalty after maturity
- **Gold Loan LTV**: Principal ≤ GoldValue × 75% (configurable)
- **Repayment Waterfall**: Penalty → Interest → Principal
- **Auto-Default**: Background job marks loans as Defaulted after 90 days past maturity
- **Due Reminders**: SMS/email at 7/3/1/0 days before maturity

---

## 🧪 Running Tests

```bash
# All tests
dotnet test ShreeJewellers.sln

# Unit tests only (fast, no external deps)
dotnet test Tests/UnitTests/

# Integration tests (uses InMemory DB)
dotnet test Tests/IntegrationTests/

# With coverage
dotnet test --collect:"XPlat Code Coverage"
```

**Test Coverage:**
- `InterestCalculationEngineTests` — 32 tests (daily/monthly/yearly, compounding, penalty, edge cases)
- `GoldLoanServiceTests` — 20 tests (create, repay, close, default check)
- `InventoryServiceTests` — 10 tests (stock deduction, reorder alerts, valuation)
- `AuthControllerTests` — 12 integration tests (register, login, lockout, JWT refresh, 403)

---

## 🔒 Security

See `Security/OWASPAudit.cs` for full OWASP Top 10 documentation.

Key controls:
- AES-256 encryption for Aadhaar/PAN numbers
- JWT access tokens (15 min) + refresh token rotation
- Account lockout after 5 failed attempts
- File upload magic-bytes validation
- Security headers middleware (CSP, HSTS, X-Frame-Options)
- Rate limiting on auth endpoints
- Full AuditLog trail for all financial transactions

---

## 📊 API Endpoints (Key)

| Group | Base Path | Auth |
|---|---|---|
| Auth | `/api/auth` | Public / Authenticated |
| Gold Loans | `/api/goldloan` | Admin/Staff/Customer |
| Products | `/api/products` | Public read / Admin write |
| Inventory | `/api/inventory` | Admin/Staff |
| Sales | `/api/sales` | Admin/Staff/Customer |
| Prices | `/api/prices` | Public read / Admin override |
| Reports | `/api/reports` | Admin only |
| Dashboard | `/api/dashboard` | Admin only |
| Settings | `/api/settings` | SuperAdmin |

Full Swagger docs available at `/swagger` in Development/Staging.

---

## 🌐 Frontend Pages

| Page | Path | Access |
|---|---|---|
| Home | `/` | Public |
| About | `/about` | Public |
| Gold Loan Info | `/gold-loan` | Public |
| Contact | `/contact` | Public |
| Login | `/account/login` | Public |
| Register (3-step) | `/account/register` | Public |
| Customer Dashboard | `/portal/dashboard` | Customer |
| My Loans | `/portal/loans` | Customer |
| My Orders | `/portal/orders` | Customer |
| My Profile | `/portal/profile` | Customer |
| Admin Dashboard | `/admin` | Admin/Staff |
| Customers | `/admin/customers` | Admin/Staff |
| Products | `/admin/products` | Admin/Staff |
| Billing | `/admin/sales` | Admin/Staff |
| Gold Loans | `/admin/loans` | Admin/Staff |
| Reports | `/admin/reports` | Admin |
| Settings | `/admin/settings` | SuperAdmin |

---

## 📦 NuGet Packages Summary

**Infrastructure**: EF Core 8, Identity, JWT, Serilog, MailKit, Twilio, QuestPDF, EPPlus, QRCoder  
**API**: JwtBearer, Swashbuckle, AspNetCoreRateLimit, FluentValidation, HealthChecks  
**Tests**: xUnit, Moq, FluentAssertions, EF Core InMemory, Bogus, WebApplicationFactory

---

## 🗓️ Deployment

See `Deployment/PRODUCTION_CHECKLIST.md` for the 50-point production readiness checklist.

```bash
# Build Docker images
docker build -f Deployment/Docker/Dockerfile.API -t shreejewellers/api .
docker build -f Deployment/Docker/Dockerfile.Web -t shreejewellers/web .

# Deploy with compose
cd Deployment/Docker
docker-compose up -d
```

GitHub Actions CI/CD pipeline (`Deployment/GitHub/ci-cd.yml`) runs on push to `main`:
1. Gitleaks secrets scan
2. NuGet vulnerability audit  
3. Build + unit + integration tests
4. Docker build & push to GHCR
5. Deploy to staging with zero-downtime rolling update

---

## 📝 Future Features

See `Features/FutureFeatures.cs` for documented stubs:
1. WhatsApp Business API — loan reminders
2. MSG91 SMS gateway — OTP delivery  
3. Camera/USB barcode scanner — billing page
4. PWA — offline dashboard
5. Customer loyalty points
6. Festival offer management
7. Public gold valuation calculator
8. Bank account integration — digital loan disbursement

---

## 📄 License

Proprietary — Shree Jewellers Internal Use Only
