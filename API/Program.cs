using AspNetCoreRateLimit;
using FluentValidation;
using FluentValidation.AspNetCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using Serilog.Events;
using ShreeJewellers.API.Middleware;
using ShreeJewellers.Application.Mappings;
using ShreeJewellers.Application.DTOs.Auth;
using ShreeJewellers.Application.Validators;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.BackgroundJobs;
using ShreeJewellers.Infrastructure.Repositories;
using ShreeJewellers.Infrastructure.Services;
using ShreeJewellers.Infrastructure.Services.Reports;
using System.Text;

// ────────────────────────────────────────────────────────────────────────────
// SERILOG — Configure early so startup errors are captured
// ────────────────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore.Database.Command", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("MachineName", Environment.MachineName)
    .Enrich.WithProperty("ThreadId", Environment.CurrentManagedThreadId)
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/shreejewellers-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    // Mask Aadhaar / PAN patterns in logs — never log raw sensitive data
    .Destructure.ByTransforming<RegisterStep2Dto>(dto => new
    {
        dto.CustomerId,
        AadhaarNumber = "XXXX-XXXX-XXXX",
        PANNumber     = "XXXXXXXXXX",
        dto.City, dto.State
    })
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog();

// ────────────────────────────────────────────────────────────────────────────
// DATABASE
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
            sqlOptions.CommandTimeout(30);
        }));

// ────────────────────────────────────────────────────────────────────────────
// ASP.NET CORE IDENTITY
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        // Password policy
        options.Password.RequiredLength         = 8;
        options.Password.RequireDigit           = true;
        options.Password.RequireLowercase       = true;
        options.Password.RequireUppercase       = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequiredUniqueChars    = 4;

        // Lockout policy — 5 failures → 15-min lockout
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan  = TimeSpan.FromMinutes(15);
        options.Lockout.AllowedForNewUsers      = true;

        // Email confirmation required before login
        options.SignIn.RequireConfirmedEmail    = false; // Set true in production
        options.SignIn.RequireConfirmedAccount  = false;

        // User settings
        options.User.RequireUniqueEmail = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddTokenProvider<DataProtectorTokenProvider<ApplicationUser>>("ShreeJewellers");

// Token lifespan for email confirmation and password reset
builder.Services.Configure<DataProtectionTokenProviderOptions>(options =>
    options.TokenLifespan = TimeSpan.FromHours(1));

// ────────────────────────────────────────────────────────────────────────────
// JWT AUTHENTICATION
// ────────────────────────────────────────────────────────────────────────────
var jwtConfig = builder.Configuration.GetSection("JwtSettings");
var secretKey  = jwtConfig["SecretKey"]
    ?? throw new InvalidOperationException("JWT SecretKey must be configured.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme    = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.SaveToken            = false;

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey         = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer           = true,
            ValidIssuer              = jwtConfig["Issuer"],
            ValidateAudience         = true,
            ValidAudience            = jwtConfig["Audience"],
            ValidateLifetime         = true,
            ClockSkew                = TimeSpan.FromMinutes(5)
        };

        // Return 401 JSON instead of redirect for API clients
        options.Events = new JwtBearerEvents
        {
            OnAuthenticationFailed = context =>
            {
                var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                logger.LogError(context.Exception, "JWT Auth Failed: {Msg}", context.Exception.Message);
                return Task.CompletedTask;
            },
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/problem+json";
                var errorMsg = context.AuthenticateFailure?.Message ?? context.ErrorDescription ?? "A valid Bearer token is required.";
                return context.Response.WriteAsync(
                    $"{{\"status\":401,\"title\":\"Unauthorized\",\"detail\":\"{errorMsg.Replace("\"", "'")}\"}}");
            },
            OnForbidden = context =>
            {
                context.Response.StatusCode  = 403;
                context.Response.ContentType = "application/problem+json";
                return context.Response.WriteAsync(
                    "{\"status\":403,\"title\":\"Forbidden\",\"detail\":\"You do not have permission to access this resource.\"}");
            }
        };
    });

// ────────────────────────────────────────────────────────────────────────────
// AUTHORIZATION POLICIES
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("KYCVerified", policy =>
        policy.RequireAssertion(ctx =>
            ctx.User.IsInRole("Admin") ||
            ctx.User.IsInRole("SuperAdmin") ||
            ctx.User.IsInRole("Staff") ||
            ctx.User.HasClaim("kycStatus", "Verified")));

    options.AddPolicy("CanManageInventory", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Staff"));

    options.AddPolicy("CanViewAllReports", policy =>
        policy.RequireRole("SuperAdmin", "Admin"));

    options.AddPolicy("CanConfigureSettings", policy =>
        policy.RequireRole("SuperAdmin"));

    options.AddPolicy("CanManageLoans", policy =>
        policy.RequireRole("SuperAdmin", "Admin", "Staff"));
});

// ────────────────────────────────────────────────────────────────────────────
// CORS — Allow only configured frontend origins
// ────────────────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? new[] { "http://localhost:5002", "https://shreejewellers.com" };

builder.Services.AddCors(options =>
    options.AddPolicy("JewellersPolicy", policy =>
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));   // Required for cookie-based refresh token

// ────────────────────────────────────────────────────────────────────────────
// RATE LIMITING (AspNetCoreRateLimit)
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.Configure<IpRateLimitOptions>(options =>
{
    options.EnableEndpointRateLimiting = true;
    options.StackBlockedRequests       = false;
    options.HttpStatusCode             = 429;
    options.GeneralRules = new List<RateLimitRule>
    {
        // Global fallback
        new() { Endpoint = "*",                           Period = "1m", Limit = 200 },
        // Login — brute force protection
        new() { Endpoint = "POST:/api/auth/login",        Period = "1m", Limit = 10  },
        // Registration
        new() { Endpoint = "POST:/api/auth/register*",    Period = "1m", Limit = 5   },
        // Forgot password
        new() { Endpoint = "POST:/api/auth/forgot-password", Period = "10m", Limit = 3 },
        // Reports
        new() { Endpoint = "*:/api/reports*",             Period = "1m", Limit = 10  },
    };
});
builder.Services.AddSingleton<IIpPolicyStore, MemoryCacheIpPolicyStore>();
builder.Services.AddSingleton<IRateLimitCounterStore, MemoryCacheRateLimitCounterStore>();
builder.Services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
builder.Services.AddSingleton<IProcessingStrategy, AsyncKeyLockProcessingStrategy>();
builder.Services.AddInMemoryRateLimiting();

// ────────────────────────────────────────────────────────────────────────────
// APPLICATION SERVICES (DI Registration)
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEncryptionService, EncryptionService>();
builder.Services.AddScoped<IFileUploadService, FileUploadService>();
builder.Services.AddScoped<IKYCService, KYCService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IBarcodeService, BarcodeService>();
builder.Services.AddScoped<IInvoiceGeneratorService, InvoiceGeneratorService>();
builder.Services.AddScoped<IInterestCalculationEngine, InterestCalculationEngine>();
builder.Services.AddScoped<IPriceService, PriceService>();
builder.Services.AddScoped<IProductService, ProductService>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<ISalesOrderService, SalesOrderService>();
builder.Services.AddScoped<IGoldLoanService, GoldLoanService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<IExcelExportService, ExcelExportService>();

builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<IInventoryRepository, InventoryRepository>();
builder.Services.AddScoped<IGoldLoanRepository, GoldLoanRepository>();
builder.Services.AddAutoMapper(typeof(AutoMapperProfile));
builder.Services.AddHostedService<LoanStatusCheckerService>();

// ────────────────────────────────────────────────────────────────────────────
// FLUENT VALIDATION
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddFluentValidationAutoValidation();
builder.Services.AddFluentValidationClientsideAdapters();
builder.Services.AddValidatorsFromAssemblyContaining<RegisterStep1Validator>();

// ────────────────────────────────────────────────────────────────────────────
// HTTP CLIENT (for GoldAPI.io price service)
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddHttpClient("GoldAPI", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["GoldPriceApi:BaseUrl"] ?? "https://www.goldapi.io/api/");
    client.DefaultRequestHeaders.Add("x-access-token", builder.Configuration["GoldPriceApi:ApiKey"]);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// ────────────────────────────────────────────────────────────────────────────
// CONTROLLERS, SWAGGER
// ────────────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition =
            System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title       = "Shree Jewellers API",
        Version     = "v1",
        Description = "Gold & Silver Jewellery Management — Inventory, Gold Loans, Sales"
    });

    // JWT Bearer authentication in Swagger UI
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name         = "Authorization",
        Type         = SecuritySchemeType.ApiKey,
        Scheme       = "Bearer",
        BearerFormat = "JWT",
        In           = ParameterLocation.Header,
        Description  = "Enter: Bearer {your_jwt_token}"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference
                { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        }
    });
});

// Health checks
builder.Services.AddHealthChecks()
    .AddSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")!,
        name: "sqlserver", tags: new[] { "db", "ready" });

// ────────────────────────────────────────────────────────────────────────────
// BUILD APP
// ────────────────────────────────────────────────────────────────────────────
var app = builder.Build();

// ────────────────────────────────────────────────────────────────────────────
// MIDDLEWARE PIPELINE (ORDER IS CRITICAL)
// ────────────────────────────────────────────────────────────────────────────

// 1. Global exception handler — must be first to catch all errors
app.UseMiddleware<GlobalExceptionMiddleware>();

// 2. Security headers — applied to every response before any processing
app.UseMiddleware<SecurityHeadersMiddleware>();

// 3. HTTPS redirection and HSTS
if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}
app.UseHttpsRedirection();

var uploadBasePath = builder.Configuration["FileStorage:BasePath"];
if (!string.IsNullOrWhiteSpace(uploadBasePath) && Directory.Exists(uploadBasePath))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(uploadBasePath),
        RequestPath = "/secure-files"
    });
}

// 4. Rate limiting
app.UseIpRateLimiting();

// 5. Serilog request logging (logs method, path, response time, status code)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms";
    // Exclude health check noise
    options.GetLevel = (ctx, elapsed, ex) =>
        ctx.Response.StatusCode > 499 || ex != null
            ? LogEventLevel.Error
            : ctx.Request.Path.StartsWithSegments("/health")
                ? LogEventLevel.Verbose
                : LogEventLevel.Information;
});

// 6. Swagger — development only
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c =>
    {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "Shree Jewellers API v1");
        c.RoutePrefix = "swagger";
    });
}

// 7. CORS
app.UseCors("JewellersPolicy");

// 8. Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

// 9. Audit logging — AFTER auth so we have user context
app.UseMiddleware<AuditLoggingMiddleware>();

// 10. Root endpoint — convenience for browser requests
//     If someone navigates to https://localhost:{port}/ the API had no route at "/"
//     which caused the 404. Add a small root mapping to redirect to Swagger in dev.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/", () => Results.Redirect("/swagger"));
}
else
{
    app.MapGet("/", () => Results.Json(new
    {
        status = "Shree Jewellers API",
        message = "API is running. Visit /health/live for liveness or contact the API owner for docs.",
        health = "/health/live"
    }));
}

// 11. Controllers
app.MapControllers();

// 12. Health checks
app.MapHealthChecks("/health/live",  new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

// ────────────────────────────────────────────────────────────────────────────
// SEED DATABASE ON FIRST RUN
// ────────────────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db          = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

    if (db.Database.IsRelational())
    {
        await db.Database.MigrateAsync();
    }
    else
    {
        await db.Database.EnsureCreatedAsync();
    }
    await SeedAsync(db, userManager, roleManager);
}

Log.Information("Shree Jewellers API started on {Environment}", app.Environment.EnvironmentName);
app.Run();

// ────────────────────────────────────────────────────────────────────────────
// SEED
// ────────────────────────────────────────────────────────────────────────────
static async Task SeedAsync(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager)
{
    // Roles
    string[] roles = { "SuperAdmin", "Admin", "Staff", "Customer" };
    foreach (var role in roles)
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    // SuperAdmin user
    const string superAdminEmail = "chandankr.mandal87@gmail.com";//"superadmin@shreejewellers.com";
    var admin = await userManager.FindByEmailAsync(superAdminEmail);
    if (admin is null)
    {
        admin = new ApplicationUser
        {
            FirstName    = "Chandan",
            LastName     = "Mandal",
            Email        = superAdminEmail,
            UserName     = superAdminEmail,
            PhoneNumber  = "9851632391",
            CustomerCode = "ADMIN-0002",
            DateOfBirth  = new DateOnly(1985, 1, 1),
            Gender       = "Male",
            AddressLine1 = "123 Jewellers Lane",
            City         = "Mumbai",
            State        = "Maharashtra",
            PinCode      = "400001",
            KYCStatus    = ShreeJewellers.Domain.Enums.KYCStatus.Verified,
            IsActive     = true,
            EmailConfirmed = true,
            CreatedAt    = DateTime.UtcNow
        };
        admin.Id = "SEED";
        await userManager.CreateAsync(admin, "Admin@123!");
    }

    if (admin != null)
    {
        if (!await userManager.IsInRoleAsync(admin, "SuperAdmin"))
            await userManager.AddToRoleAsync(admin, "SuperAdmin");
        if (!await userManager.IsInRoleAsync(admin, "Admin"))
            await userManager.AddToRoleAsync(admin, "Admin");
    }

    // Default Interest Setting
    if (!db.InterestSettings.Any())
    {
        db.InterestSettings.Add(new ShreeJewellers.Domain.Entities.InterestSetting
        {
            InterestRatePercent  = 2.00m,
            CalculationType      = ShreeJewellers.Domain.Enums.InterestCalculationType.Monthly,
            CompoundingEnabled   = false,
            PenaltyRatePercent   = 1.00m,
            DefaultTenureMonths  = 12,
            LoanToValuePercent   = 75.00m,
            EffectiveFrom        = DateOnly.FromDateTime(DateTime.Today),
            EffectiveTo          = null,
            CreatedByUserId      = "ffd75c58-b8e5-4181-a0d1-068929fbebcc",//"SEED",
            CreatedAt            = DateTime.UtcNow
        });
    }

    // Default App Settings
    var defaults = new Dictionary<string, (string value, string desc)>
    {
        ["ShopName"]        = ("Shree Jewellers", "Shop display name"),
        ["ShopAddress"]     = ("123 Gold Market, Mumbai, Maharashtra - 400001", "Full shop address"),
        ["GSTNumber"]       = ("27XXXXX1234X1ZX", "GSTIN for invoices"),
        ["ShopPhone"]       = ("+91-22-12345678", "Customer-facing phone number"),
        ["ShopEmail"]       = ("info@shreejewellers.com", "Contact email"),
        ["LogoUrl"]         = ("/images/logo.png", "Logo image URL"),
        ["DefaultCurrency"] = ("INR", "Default currency code"),
        ["SMSEnabled"]      = ("true", "Enable/disable SMS notifications"),
        ["EmailEnabled"]    = ("true", "Enable/disable email notifications"),
        ["WhatsAppEnabled"] = ("false", "Enable/disable WhatsApp Business API"),
        ["LoyaltyPointsPerRupee"] = ("0.01", "Points earned per rupee spent"),
    };

    foreach (var (key, (value, desc)) in defaults)
    {
        if (!db.AppSettings.Any(s => s.SettingKey == key))
        {
            db.AppSettings.Add(new ShreeJewellers.Domain.Entities.AppSetting
            {
                SettingKey   = key,
                SettingValue = value,
                Description  = desc,
                UpdatedAt    = DateTime.UtcNow
            });
        }
    }

    // Default Categories
    var categories = new[]
    {
        ("Gold Jewellery",  ShreeJewellers.Domain.Enums.MetalType.Gold,     "22K and 24K gold jewellery items"),
        ("Silver Items",    ShreeJewellers.Domain.Enums.MetalType.Silver,   "Sterling silver and pure silver items"),
        ("Diamond",         ShreeJewellers.Domain.Enums.MetalType.Diamond,  "Diamond studded jewellery"),
        ("Platinum",        ShreeJewellers.Domain.Enums.MetalType.Platinum, "Platinum jewellery"),
        ("Gold Coins/Bars", ShreeJewellers.Domain.Enums.MetalType.Gold,     "Investment gold coins and bars"),
        ("Silver Coins",    ShreeJewellers.Domain.Enums.MetalType.Silver,   "Silver coins and bars"),
    };

    foreach (var (name, type, desc) in categories)
    {
        if (!db.Categories.Any(c => c.Name == name))
        {
            db.Categories.Add(new ShreeJewellers.Domain.Entities.Category
            {
                Name        = name,
                MetalType   = type,
                Description = desc,
                IsActive    = true,
                CreatedAt   = DateTime.UtcNow
            });
        }
    }

    await db.SaveChangesAsync();

    // Comprehensive realistic sample business data
    await SeedSampleBusinessDataAsync(db, userManager);

    Log.Information("Database seeded successfully.");
}

static async Task SeedSampleBusinessDataAsync(
    ApplicationDbContext db,
    UserManager<ApplicationUser> userManager)
{
    // 0. Clean any 0-value price records
    var zeroRecords = db.GoldPriceHistory.Where(p => p.GoldRatePer10Gram_24K <= 0 || p.GoldRatePer10Gram_22K <= 0);
    if (zeroRecords.Any())
    {
        db.GoldPriceHistory.RemoveRange(zeroRecords);
        await db.SaveChangesAsync();
    }

    // 1. Seed Gold Price History if empty or lacks positive rates
    if (!db.GoldPriceHistory.Any(p => p.GoldRatePer10Gram_24K > 0))
    {
        var now = DateTime.UtcNow;
        var historicalRates = new[]
        {
            (now.AddDays(-6), 71800.00m, 65800.00m, 86500.00m),
            (now.AddDays(-5), 72000.00m, 66000.00m, 87000.00m),
            (now.AddDays(-4), 72150.00m, 66140.00m, 87200.00m),
            (now.AddDays(-3), 72300.00m, 66280.00m, 87500.00m),
            (now.AddDays(-2), 72400.00m, 66370.00m, 87800.00m),
            (now.AddDays(-1), 72450.00m, 66410.00m, 87900.00m),
            (now,             72500.00m, 66460.00m, 88000.00m),
        };
        foreach (var (date, r24, r22, rSil) in historicalRates)
        {
            db.GoldPriceHistory.Add(new ShreeJewellers.Domain.Entities.GoldPriceHistory
            {
                RecordedAt = date,
                GoldRatePer10Gram_24K = r24,
                GoldRatePer10Gram_22K = r22,
                SilverRatePerKg = rSil,
                Source = ShreeJewellers.Domain.Enums.PriceSource.Manual,
                IsManualOverride = true,
                CreatedByUserId = "SEED"
            });
        }
        await db.SaveChangesAsync();
    }

    // 2. Seed Staff and Customers
    var staffEmail = "staff@shreejewellers.com";
    if (await userManager.FindByEmailAsync(staffEmail) is null)
    {
        var staffCode = "STAFF-0001";
        if (await db.Users.AnyAsync(u => u.CustomerCode == staffCode))
        {
            staffCode = $"STAFF-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
        }

        var staff = new ApplicationUser
        {
            FirstName = "Rahul",
            LastName = "Sharma",
            Email = staffEmail,
            UserName = staffEmail,
            PhoneNumber = "9820123456",
            CustomerCode = staffCode,
            DateOfBirth = new DateOnly(1992, 5, 10),
            Gender = "Male",
            AddressLine1 = "Shop 4, Gold Bazaar",
            City = "Mumbai",
            State = "Maharashtra",
            PinCode = "400001",
            KYCStatus = ShreeJewellers.Domain.Enums.KYCStatus.Verified,
            IsActive = true,
            EmailConfirmed = true,
            CreatedAt = DateTime.UtcNow
        };
        await userManager.CreateAsync(staff, "Staff@123!");
        await userManager.AddToRoleAsync(staff, "Staff");
    }

    var sampleCustomers = new[]
    {
        ("priya.patel@gmail.com", "Priya", "Patel", "9876543210", "Female", "CUST-2026-0001", "Flat 402, Lotus Tower", new DateOnly(1991, 3, 14)),
        ("amit.verma@gmail.com", "Amit", "Verma", "9819283746", "Male", "CUST-2026-0002", "B-12, Diamond Enclave", new DateOnly(1987, 8, 22)),
        ("sneha.deshmukh@gmail.com", "Sneha", "Deshmukh", "9730192837", "Female", "CUST-2026-0003", "15, Sunshine Heights", new DateOnly(1995, 11, 5)),
        ("vikram.singh@gmail.com", "Vikram", "Singh", "9845112233", "Male", "CUST-2026-0004", "7A, Royal Residency", new DateOnly(1984, 1, 19))
    };

    foreach (var (email, first, last, phone, gender, code, addr, dob) in sampleCustomers)
    {
        if (await userManager.FindByEmailAsync(email) is null)
        {
            var assignedCode = code;
            if (await db.Users.AnyAsync(u => u.CustomerCode == assignedCode))
            {
                assignedCode = $"CUST-{DateTime.Today.Year}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";
            }

            var cust = new ApplicationUser
            {
                FirstName = first,
                LastName = last,
                Email = email,
                UserName = email,
                PhoneNumber = phone,
                CustomerCode = assignedCode,
                DateOfBirth = dob,
                Gender = gender,
                AddressLine1 = addr,
                City = "Mumbai",
                State = "Maharashtra",
                PinCode = "400001",
                KYCStatus = ShreeJewellers.Domain.Enums.KYCStatus.Verified,
                IsActive = true,
                EmailConfirmed = true,
                CreatedAt = DateTime.UtcNow
            };
            await userManager.CreateAsync(cust, "Customer@123!");
            await userManager.AddToRoleAsync(cust, "Customer");
        }
    }

    // 3. Seed Products if realistic items not yet present
    if (!db.Products.Any(p => p.SKUCode.StartsWith("GLD-")))
    {
        var goldJewelleryCat = db.Categories.FirstOrDefault(c => c.Name == "Gold Jewellery") ?? db.Categories.First();
        var silverCoinsCat = db.Categories.FirstOrDefault(c => c.Name == "Silver Coins") ?? db.Categories.First();
        var diamondCat = db.Categories.FirstOrDefault(c => c.Name == "Diamond") ?? db.Categories.First();
        var goldCoinsCat = db.Categories.FirstOrDefault(c => c.Name == "Gold Coins/Bars") ?? db.Categories.First();

        var productsToSeed = new[]
        {
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldJewelleryCat.Id,
                Name = "22K Gold Temple Choker Necklace",
                Description = "Intricately crafted antique finish temple jewellery choker with Lakshmi motif.",
                SKUCode = "GLD-22K-NCK-001",
                HSNCode = "7113",
                Purity = "22K",
                GrossWeightGrams = 35.500m,
                NetWeightGrams = 35.500m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 450.00m,
                HallmarkNumber = "HM-22K-9812",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 5,
                ReorderLevel = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldJewelleryCat.Id,
                Name = "22K Royal Bridal Kada Bangles (Pair)",
                Description = "Heavy bridal kadas with filigree floral carvings and screw lock.",
                SKUCode = "GLD-22K-BNG-002",
                HSNCode = "7113",
                Purity = "22K",
                GrossWeightGrams = 48.200m,
                NetWeightGrams = 48.200m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 500.00m,
                HallmarkNumber = "HM-22K-9813",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 3,
                ReorderLevel = 1,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = diamondCat.Id,
                Name = "18K Solitaire Diamond Ring (0.60 ct)",
                Description = "VVS-EF grade certified natural diamond mounted in 18K white-yellow gold band.",
                SKUCode = "DMD-18K-RNG-003",
                HSNCode = "7113",
                Purity = "18K",
                GrossWeightGrams = 4.800m,
                NetWeightGrams = 4.200m,
                StoneWeightGrams = 0.600m,
                StoneValue = 45000.00m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.Percentage,
                MakingChargesValue = 12.00m,
                HallmarkNumber = "HM-18K-D772",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 8,
                ReorderLevel = 3,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldJewelleryCat.Id,
                Name = "22K Traditional Gold Mangalsutra (36 inch)",
                Description = "Double chain black beaded auspicious mangalsutra with 22K gold floral pendant.",
                SKUCode = "GLD-22K-MNG-004",
                HSNCode = "7113",
                Purity = "22K",
                GrossWeightGrams = 18.500m,
                NetWeightGrams = 16.500m,
                StoneWeightGrams = 2.000m,
                StoneValue = 3500.00m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 420.00m,
                HallmarkNumber = "HM-22K-9814",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 10,
                ReorderLevel = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = silverCoinsCat.Id,
                Name = "999 Pure Silver Laxmi Ganesh Coin (100g)",
                Description = "Auspicious 999 fine sterling silver embossed religious coin with tamper-proof blister pack.",
                SKUCode = "SLV-999-CON-005",
                HSNCode = "7114",
                Purity = "999",
                GrossWeightGrams = 100.000m,
                NetWeightGrams = 100.000m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 8.00m,
                HallmarkCharges = 0m,
                GSTRatePercent = 3.00m,
                StockQuantity = 25,
                ReorderLevel = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldJewelleryCat.Id,
                Name = "22K Men's Italian Figaro Gold Chain",
                Description = "Sturdy Italian design Figaro chain for men with lobster claw clasp.",
                SKUCode = "GLD-22K-CHN-006",
                HSNCode = "7113",
                Purity = "22K",
                GrossWeightGrams = 24.000m,
                NetWeightGrams = 24.000m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 380.00m,
                HallmarkNumber = "HM-22K-9815",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 6,
                ReorderLevel = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldCoinsCat.Id,
                Name = "24K 999 Pure Investment Gold Bar (10g)",
                Description = "Swiss mint certified 999.9 fine pure gold bullion bar with assay cert.",
                SKUCode = "GLD-24K-BAR-007",
                HSNCode = "7108",
                Purity = "24K",
                GrossWeightGrams = 10.000m,
                NetWeightGrams = 10.000m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 150.00m,
                HallmarkCharges = 0m,
                GSTRatePercent = 3.00m,
                StockQuantity = 15,
                ReorderLevel = 5,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            },
            new ShreeJewellers.Domain.Entities.Product
            {
                CategoryId = goldJewelleryCat.Id,
                Name = "22K Designer Peacock Gold Jhumka Earrings",
                Description = "Traditional South-Indian heritage peacock bell earrings with hanging pearls.",
                SKUCode = "GLD-22K-JHM-008",
                HSNCode = "7113",
                Purity = "22K",
                GrossWeightGrams = 14.200m,
                NetWeightGrams = 14.200m,
                MakingChargesType = ShreeJewellers.Domain.Enums.MakingChargesType.PerGram,
                MakingChargesValue = 480.00m,
                HallmarkNumber = "HM-22K-9816",
                HallmarkCharges = 45.00m,
                GSTRatePercent = 3.00m,
                StockQuantity = 7,
                ReorderLevel = 2,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            }
        };

        db.Products.AddRange(productsToSeed);
        await db.SaveChangesAsync();

        foreach (var p in productsToSeed)
        {
            db.InventoryTransactions.Add(new ShreeJewellers.Domain.Entities.InventoryTransaction
            {
                ProductId = p.Id,
                Quantity = p.StockQuantity,
                WeightGrams = p.GrossWeightGrams * p.StockQuantity,
                RatePerGram = 6650m,
                TotalValue = p.GrossWeightGrams * p.StockQuantity * 6650m,
                TransactionType = ShreeJewellers.Domain.Enums.TransactionType.Purchase,
                ReferenceNo = "INITIAL-STOCK-001",
                Notes = "Opening inventory balance",
                CreatedByUserId = "SEED",
                CreatedAt = DateTime.UtcNow
            });
        }
        await db.SaveChangesAsync();
    }

    // 4. Seed Sales Orders for Today if not yet present
    if (!db.SalesOrders.Any(o => o.OrderDate.Date == DateTime.UtcNow.Date))
    {
        var priya = await userManager.FindByEmailAsync("priya.patel@gmail.com") ?? await userManager.Users.FirstAsync();
        var amit = await userManager.FindByEmailAsync("amit.verma@gmail.com") ?? await userManager.Users.Skip(1).FirstAsync();
        var p1 = db.Products.FirstOrDefault(p => p.SKUCode == "GLD-22K-NCK-001") ?? db.Products.First();
        var p7 = db.Products.FirstOrDefault(p => p.SKUCode == "GLD-24K-BAR-007") ?? db.Products.Skip(1).First();

        if (p1 != null)
        {
            var gross1 = (35.500m * 6646m) + (35.500m * 450m) + 45m;
            var tax1 = Math.Round(gross1 * 0.03m, 2);
            var net1 = gross1 + tax1;

            var order1 = new ShreeJewellers.Domain.Entities.SalesOrder
            {
                OrderNumber = $"INV-{DateTime.UtcNow:yyyy-MM}-9001",
                OrderDate = DateTime.UtcNow,
                CustomerUserId = priya.Id,
                CreatedByUserId = "SEED",
                GrossAmount = gross1,
                DiscountAmount = 2000m,
                TaxAmount = tax1,
                CGSTAmount = Math.Round(tax1 / 2, 2),
                SGSTAmount = Math.Round(tax1 / 2, 2),
                NetAmount = net1 - 2000m,
                AmountPaid = net1 - 2000m,
                PaymentMode = ShreeJewellers.Domain.Enums.PaymentMode.UPI,
                PaymentStatus = ShreeJewellers.Domain.Enums.PaymentStatus.Paid,
                Status = ShreeJewellers.Domain.Enums.OrderStatus.Confirmed,
                Notes = "Walk-in purchase, UPI payment confirmed.",
                CreatedAt = DateTime.UtcNow,
                Items = new List<ShreeJewellers.Domain.Entities.SalesOrderItem>
                {
                    new ShreeJewellers.Domain.Entities.SalesOrderItem
                    {
                        ProductId = p1.Id,
                        Quantity = 1,
                        WeightGrams = 35.500m,
                        RatePerGram = 6646m,
                        MakingCharges = 35.500m * 450m,
                        HallmarkCharges = 45m,
                        LineTotal = gross1
                    }
                }
            };
            db.SalesOrders.Add(order1);
        }

        if (p7 != null)
        {
            var gross2 = (10.000m * 7250m) + (10.000m * 150m);
            var tax2 = Math.Round(gross2 * 0.03m, 2);
            var net2 = gross2 + tax2;

            var order2 = new ShreeJewellers.Domain.Entities.SalesOrder
            {
                OrderNumber = $"INV-{DateTime.UtcNow:yyyy-MM}-9002",
                OrderDate = DateTime.UtcNow,
                CustomerUserId = amit.Id,
                CreatedByUserId = "SEED",
                GrossAmount = gross2,
                DiscountAmount = 0m,
                TaxAmount = tax2,
                CGSTAmount = Math.Round(tax2 / 2, 2),
                SGSTAmount = Math.Round(tax2 / 2, 2),
                NetAmount = net2,
                AmountPaid = net2,
                PaymentMode = ShreeJewellers.Domain.Enums.PaymentMode.Cash,
                PaymentStatus = ShreeJewellers.Domain.Enums.PaymentStatus.Paid,
                Status = ShreeJewellers.Domain.Enums.OrderStatus.Delivered,
                Notes = "Bullion bar purchase delivered over counter.",
                CreatedAt = DateTime.UtcNow,
                Items = new List<ShreeJewellers.Domain.Entities.SalesOrderItem>
                {
                    new ShreeJewellers.Domain.Entities.SalesOrderItem
                    {
                        ProductId = p7.Id,
                        Quantity = 1,
                        WeightGrams = 10.000m,
                        RatePerGram = 7250m,
                        MakingCharges = 1500m,
                        LineTotal = gross2
                    }
                }
            };
            db.SalesOrders.Add(order2);
        }

        await db.SaveChangesAsync();
    }

    // 5. Seed Gold Loans if sample loans not yet present
    if (!db.GoldLoans.Any(l => l.LoanNumber.StartsWith("GL-2026-")))
    {
        var priya = await userManager.FindByEmailAsync("priya.patel@gmail.com");
        var amit = await userManager.FindByEmailAsync("amit.verma@gmail.com");
        var vikram = await userManager.FindByEmailAsync("vikram.singh@gmail.com");
        var interestSetting = db.InterestSettings.FirstOrDefault() ?? new ShreeJewellers.Domain.Entities.InterestSetting
        {
            InterestRatePercent = 2.00m,
            CalculationType = ShreeJewellers.Domain.Enums.InterestCalculationType.Monthly,
            CompoundingEnabled = false,
            PenaltyRatePercent = 1.00m,
            DefaultTenureMonths = 12,
            LoanToValuePercent = 75.00m,
            EffectiveFrom = DateOnly.FromDateTime(DateTime.Today),
            CreatedByUserId = "SEED"
        };

        if (interestSetting.Id == 0)
        {
            db.InterestSettings.Add(interestSetting);
            await db.SaveChangesAsync();
        }

        var today = DateOnly.FromDateTime(DateTime.Today);

        // Loan 1: Active
        if (priya != null)
        {
            var loan1 = new ShreeJewellers.Domain.Entities.GoldLoan
            {
                CustomerUserId = priya.Id,
                CreatedByUserId = "SEED",
                InterestSettingId = interestSetting.Id,
                LoanNumber = "GL-2026-0001",
                LoanDate = today.AddDays(-30),
                MaturityDate = today.AddMonths(11),
                GoldDepositedWeightGrams = 32.000m,
                GoldPurity = "22K",
                GoldCurrentValueAtDeposit = 212672.00m,
                PrincipalAmount = 150000.00m,
                LoanToValuePercent = 75.00m,
                TotalRepaid = 3000.00m,
                LoanStatus = ShreeJewellers.Domain.Enums.LoanStatus.Active,
                CreatedAt = DateTime.UtcNow,
                GoldItems = new List<ShreeJewellers.Domain.Entities.GoldLoanItem>
                {
                    new ShreeJewellers.Domain.Entities.GoldLoanItem
                    {
                        ItemDescription = "Pair of 22K Solid Gold Carved Bangles",
                        WeightGrams = 32.000m,
                        Purity = "22K",
                        EstimatedValue = 212672.00m,
                        HallmarkNumber = "HM-22K-PLEDGE-01"
                    }
                }
            };
            db.GoldLoans.Add(loan1);
            await db.SaveChangesAsync();

            db.LoanRepayments.Add(new ShreeJewellers.Domain.Entities.LoanRepayment
            {
                GoldLoanId = loan1.Id,
                CreatedByUserId = "SEED",
                RepaymentDate = today.AddDays(-1),
                AmountPaid = 3000.00m,
                PrincipalComponent = 0m,
                InterestComponent = 3000.00m,
                PenaltyAmount = 0m,
                PaymentMode = ShreeJewellers.Domain.Enums.PaymentMode.UPI,
                ReceiptNumber = "RCP-2026-0001",
                Notes = "First month interest serviced via UPI",
                PrincipalBalanceAfter = 150000.00m,
                CreatedAt = DateTime.UtcNow
            });
        }

        // Loan 2: Due This Week
        if (amit != null)
        {
            var loan2 = new ShreeJewellers.Domain.Entities.GoldLoan
            {
                CustomerUserId = amit.Id,
                CreatedByUserId = "SEED",
                InterestSettingId = interestSetting.Id,
                LoanNumber = "GL-2026-0002",
                LoanDate = today.AddMonths(-6).AddDays(3),
                MaturityDate = today.AddDays(3),
                GoldDepositedWeightGrams = 18.500m,
                GoldPurity = "22K",
                GoldCurrentValueAtDeposit = 122951.00m,
                PrincipalAmount = 75000.00m,
                LoanToValuePercent = 75.00m,
                TotalRepaid = 0m,
                LoanStatus = ShreeJewellers.Domain.Enums.LoanStatus.Active,
                CreatedAt = DateTime.UtcNow,
                GoldItems = new List<ShreeJewellers.Domain.Entities.GoldLoanItem>
                {
                    new ShreeJewellers.Domain.Entities.GoldLoanItem
                    {
                        ItemDescription = "22K Gold Rope Chain (24 inch)",
                        WeightGrams = 18.500m,
                        Purity = "22K",
                        EstimatedValue = 122951.00m,
                        HallmarkNumber = "HM-22K-PLEDGE-02"
                    }
                }
            };
            db.GoldLoans.Add(loan2);
        }

        // Loan 3: Defaulted
        if (vikram != null)
        {
            var loan3 = new ShreeJewellers.Domain.Entities.GoldLoan
            {
                CustomerUserId = vikram.Id,
                CreatedByUserId = "SEED",
                InterestSettingId = interestSetting.Id,
                LoanNumber = "GL-2026-0003",
                LoanDate = today.AddMonths(-8),
                MaturityDate = today.AddDays(-60),
                GoldDepositedWeightGrams = 45.000m,
                GoldPurity = "22K",
                GoldCurrentValueAtDeposit = 299070.00m,
                PrincipalAmount = 180000.00m,
                LoanToValuePercent = 75.00m,
                TotalRepaid = 0m,
                LoanStatus = ShreeJewellers.Domain.Enums.LoanStatus.Defaulted,
                CreatedAt = DateTime.UtcNow,
                GoldItems = new List<ShreeJewellers.Domain.Entities.GoldLoanItem>
                {
                    new ShreeJewellers.Domain.Entities.GoldLoanItem
                    {
                        ItemDescription = "Antique Gold Haar Necklace (45g)",
                        WeightGrams = 45.000m,
                        Purity = "22K",
                        EstimatedValue = 299070.00m,
                        HallmarkNumber = "HM-22K-PLEDGE-03"
                    }
                }
            };
            db.GoldLoans.Add(loan3);
        }

        await db.SaveChangesAsync();
    }
}


public partial class Program { }
