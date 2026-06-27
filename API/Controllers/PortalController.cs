using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Portal;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Services;
using System.Security.Claims;

namespace ShreeJewellers.API.Controllers;

[ApiController]
[Route("api/public")]
[Produces("application/json")]
public class PublicController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly INotificationService _notificationService;

    public PublicController(ApplicationDbContext db, INotificationService notificationService)
    {
        _db = db;
        _notificationService = notificationService;
    }

    [HttpGet("home")]
    [AllowAnonymous]
    public async Task<IActionResult> GetHome()
    {
        var settings = await LoadSettingsAsync();
        var categories = await _db.Categories
            .Include(c => c.Products)
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .Take(4)
            .Select(c => new PublicCategoryCardDto(
                c.Id,
                c.Name,
                c.MetalType.ToString(),
                c.Products.Count(p => p.IsActive),
                c.Description ?? string.Empty))
            .ToListAsync();

        var trustedSinceYear = int.TryParse(settings.TrustedSinceYear, out var sinceYear) ? sinceYear : 1998;
        var dto = new PublicHomeDto(
            settings.ShopName,
            trustedSinceYear,
            $"Trusted Since {trustedSinceYear} — Gold & Silver Jewellery",
            "Fine craftsmanship, transparent pricing, and secure gold loan services under one trusted roof.",
            settings.AboutSummary,
            new[]
            {
                new PublicFeatureDto("BIS Hallmark Certified", "Purity you can verify with confidence.", "bi-patch-check"),
                new PublicFeatureDto("Trusted Gold Loans", "Fast processing with clear interest terms.", "bi-shield-lock"),
                new PublicFeatureDto("Transparent Billing", "GST-ready invoices and honest pricing.", "bi-receipt")
            },
            categories,
            new[]
            {
                new TestimonialDto("Anita Sharma", "Beautiful craftsmanship and very courteous staff.", "Kolkata"),
                new TestimonialDto("Rakesh Gupta", "The gold loan process was fast and completely transparent.", "Howrah"),
                new TestimonialDto("Mitali Das", "I trust them for family purchases and hallmark quality.", "Durgapur")
            },
            new[]
            {
                new QuickLinkDto("Shop", "/shop"),
                new QuickLinkDto("Gold Loan", "/gold-loan"),
                new QuickLinkDto("Contact", "/contact")
            });

        return Ok(dto);
    }

    [HttpGet("about")]
    [AllowAnonymous]
    public async Task<IActionResult> GetAbout()
    {
        var settings = await LoadSettingsAsync();
        return Ok(new PublicAboutDto(
            settings.ShopName,
            int.TryParse(settings.TrustedSinceYear, out var sinceYear) ? sinceYear : 1998,
            settings.AboutStory,
            settings.OwnerName,
            settings.OwnerTitle,
            settings.OwnerPhotoUrl,
            new[] { "BIS Hallmark Certified", "GST Registered", "Trusted Local Gold Loan Service" },
            "Every gold ornament is handled with hallmark-first transparency and careful documentation."
        ));
    }

    [HttpGet("contact")]
    [AllowAnonymous]
    public async Task<IActionResult> GetContact()
    {
        var settings = await LoadSettingsAsync();
        return Ok(new PublicContactDto(
            settings.ShopName,
            settings.ShopAddress,
            settings.ShopPhone,
            settings.ShopEmail,
            settings.WhatsAppNumber,
            settings.MapEmbedUrl,
            settings.BusinessHours
        ));
    }

    [HttpGet("gold-loan-info")]
    [AllowAnonymous]
    public async Task<IActionResult> GetGoldLoanInfo()
    {
        var setting = await _db.InterestSettings
            .Where(s => s.EffectiveTo == null)
            .OrderByDescending(s => s.EffectiveFrom)
            .FirstOrDefaultAsync();

        if (setting is null)
        {
            return Ok(new PublicGoldLoanInfoDto(
                0, "Monthly", 12, 0,
                Array.Empty<string>(),
                Array.Empty<string>()));
        }

        return Ok(new PublicGoldLoanInfoDto(
            setting.InterestRatePercent,
            setting.CalculationType.ToString(),
            setting.DefaultTenureMonths,
            setting.LoanToValuePercent,
            new[]
            {
                "Bring your BIS hallmarked gold ornaments and a valid ID proof.",
                "Our staff weighs and documents each pledged item with transparency.",
                "Eligible loan amount is calculated against the current gold rate and approved LTV.",
                "Loan is disbursed after verification, and you can track repayments in your account."
            },
            new[]
            {
                "Aadhaar / PAN",
                "Address proof",
                "Mobile number",
                "Passport-size photo"
            }
        ));
    }

    [HttpPost("contact-inquiry")]
    [AllowAnonymous]
    public async Task<IActionResult> CreateContactInquiry([FromBody] ContactInquiryDto dto)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = "CONTACT_INQUIRY",
            EntityName = "ContactInquiry",
            NewValues = System.Text.Json.JsonSerializer.Serialize(dto),
            Timestamp = DateTime.UtcNow,
            IPAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
        });
        await _db.SaveChangesAsync();

        var settings = await LoadSettingsAsync();
        await _notificationService.SendEmailAsync(
            settings.ShopEmail,
            $"New contact inquiry from {dto.Name}",
            $"Name: {dto.Name}\nPhone: {dto.Phone}\nEmail: {dto.Email}\n\nMessage:\n{dto.Message}");

        return Ok(new { message = "Thank you. Your message has been received successfully." });
    }

    private async Task<ShopSettingsDto> LoadSettingsAsync()
    {
        var map = await _db.AppSettings.ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        string Get(string key, string fallback) => map.TryGetValue(key, out var value) ? value : fallback;

        return new ShopSettingsDto(
            Get("ShopName", "Shree Jewellers"),
            Get("ShopAddress", "123 Gold Market, Mumbai, Maharashtra - 400001"),
            Get("GSTNumber", "27XXXXX1234X1ZX"),
            Get("ShopPhone", "+91-22-12345678"),
            Get("ShopEmail", "info@shreejewellers.com"),
            Get("LogoUrl", ""),
            Get("WhatsAppNumber", "919851632391"),
            Get("MapEmbedUrl", "https://www.google.com/maps"),
            Get("TrustedSinceYear", "1998"),
            Get("BusinessHours", "Mon - Sat, 10:00 AM - 8:00 PM"),
            Get("AboutSummary", "A family jeweller known for purity, service, and long-term trust."),
            Get("AboutStory", "For decades, we have served families with hallmark jewellery, fair pricing, and dependable gold loan support."),
            Get("OwnerName", "Chandan Mandal"),
            Get("OwnerTitle", "Founder"),
            Get("OwnerPhotoUrl", "")
        );
    }
}

[ApiController]
[Route("api/portal")]
[Authorize]
[Produces("application/json")]
public class PortalController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IFileUploadService _fileUploadService;

    public PortalController(
        ApplicationDbContext db,
        IFileUploadService fileUploadService)
    {
        _db = db;
        _fileUploadService = fileUploadService;
    }

    [HttpGet("customer/dashboard")]
    [Authorize(Roles = "Customer")]
    public async Task<IActionResult> GetCustomerDashboard()
    {
        var userId = GetUserId();
        var user = await _db.Users.FirstAsync(u => u.Id == userId);
        var today = DateOnly.FromDateTime(DateTime.Today);

        var loans = await _db.GoldLoans
            .Include(l => l.Repayments)
            .Include(l => l.InterestSetting)
            .Where(l => l.CustomerUserId == userId)
            .OrderByDescending(l => l.LoanDate)
            .ToListAsync();

        var orders = await _db.SalesOrders
            .Where(o => o.CustomerUserId == userId)
            .OrderByDescending(o => o.OrderDate)
            .Take(5)
            .ToListAsync();

        var activeLoans = loans
            .Where(l => l.LoanStatus is LoanStatus.Active or LoanStatus.PartiallyRepaid or LoanStatus.Extended)
            .Select(l =>
            {
                var principalRemaining = l.PrincipalAmount - l.Repayments.Sum(r => r.PrincipalComponent);
                var accruedInterest = l.InterestSetting is null
                    ? 0m
                    : Math.Round(
                        principalRemaining * (l.InterestSetting.InterestRatePercent / 100m) *
                        Math.Max(0, today.DayNumber - l.LoanDate.DayNumber) /
                        (l.InterestSetting.CalculationType == InterestCalculationType.Yearly ? 365m : 30m),
                        2);

                return new LoanDashboardCardDto(
                    l.Id,
                    l.LoanNumber,
                    l.PrincipalAmount,
                    Math.Max(0, l.MaturityDate.DayNumber - today.DayNumber),
                    accruedInterest,
                    Math.Round(principalRemaining + accruedInterest, 2),
                    l.LoanStatus.ToString());
            })
            .ToList();

        return Ok(new CustomerDashboardDto(
            user.Id,
            user.FullName,
            user.CustomerCode ?? string.Empty,
            user.KYCStatus.ToString(),
            activeLoans.Count,
            activeLoans.Sum(x => x.OutstandingBalance),
            activeLoans.Sum(x => x.InterestAccrued),
            orders.Count,
            orders.Sum(o => o.NetAmount),
            activeLoans,
            orders.Select(o => new OrderDashboardCardDto(
                o.Id,
                o.OrderNumber,
                DateOnly.FromDateTime(o.OrderDate),
                o.NetAmount,
                o.Status.ToString(),
                o.PaymentStatus.ToString()))
        ));
    }

    [HttpGet("profile")]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _db.Users.FirstAsync(u => u.Id == GetUserId());
        return Ok(ToProfileDto(user));
    }

    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == GetUserId());
        user.FirstName = dto.FirstName.Trim();
        user.LastName = dto.LastName.Trim();
        user.PhoneNumber = dto.PhoneNumber.Trim();
        user.AlternatePhone = dto.AlternatePhone?.Trim();
        if (dto.DateOfBirth.HasValue) user.DateOfBirth = dto.DateOfBirth.Value;
        if (!string.IsNullOrWhiteSpace(dto.Gender)) user.Gender = dto.Gender.Trim();
        user.AddressLine1 = dto.AddressLine1.Trim();
        user.AddressLine2 = dto.AddressLine2?.Trim();
        user.City = dto.City.Trim();
        user.State = dto.State.Trim();
        user.PinCode = dto.PinCode.Trim();
        user.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return Ok(ToProfileDto(user));
    }

    [HttpPost("profile/documents")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadProfileDocuments(IFormFile? profilePhoto, IFormFile? idProof)
    {
        var user = await _db.Users.FirstAsync(u => u.Id == GetUserId());

        if (profilePhoto is not null)
        {
            if (!_fileUploadService.IsValidFile(profilePhoto, out var profileError))
                return BadRequest(new { message = profileError });

            user.ProfilePhotoUrl = await _fileUploadService.UploadAsync(profilePhoto, "profile", user.Id);
        }

        if (idProof is not null)
        {
            if (!_fileUploadService.IsValidFile(idProof, out var proofError))
                return BadRequest(new { message = proofError });

            user.IDProofUrl = await _fileUploadService.UploadAsync(idProof, "kyc-docs", user.Id);
        }

        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        return Ok(ToProfileDto(user));
    }

    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new UnauthorizedAccessException("User identity not found.");

    private static ProfileDto ToProfileDto(ApplicationUser user) =>
        new(
            user.Id,
            user.FirstName,
            user.LastName,
            user.FullName,
            user.Email ?? string.Empty,
            user.PhoneNumber ?? string.Empty,
            user.AlternatePhone,
            user.DateOfBirth,
            user.Gender,
            user.AddressLine1,
            user.AddressLine2,
            user.City,
            user.State,
            user.PinCode,
            user.CustomerCode,
            user.KYCStatus.ToString(),
            user.ProfilePhotoUrl,
            user.IDProofUrl,
            user.KYCRejectionReason);
}
