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
            ClockSkew                = TimeSpan.Zero   // No grace period — token expiry is exact
        };

        // Return 401 JSON instead of redirect for API clients
        options.Events = new JwtBearerEvents
        {
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode  = 401;
                context.Response.ContentType = "application/problem+json";
                return context.Response.WriteAsync(
                    "{\"status\":401,\"title\":\"Unauthorized\",\"detail\":\"A valid Bearer token is required.\"}");
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

    await db.Database.MigrateAsync();
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
    if (await userManager.FindByEmailAsync(superAdminEmail) is null)
    {
        var admin = new ApplicationUser
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
        // then CreatedByUserId = "SEED" will satisfy FK
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
    Log.Information("Database seeded successfully.");
}
