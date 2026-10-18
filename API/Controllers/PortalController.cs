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
            $"Trusted Since {trustedSinceYear} — Fine Gold, Diamonds & Instant Loans",
            "Artisanal 22K/24K hallmark purity, certified solitaires, transparent digital billing, and confidential gold loans under one revered roof.",
            settings.AboutSummary,
            new[]
            {
                new PublicFeatureDto("100% BIS 916 Hallmark", "Every ornament carries laser-etched HUID certification verifiable on the BIS Care portal.", "bi-patch-check"),
                new PublicFeatureDto("Instant Secure Gold Loans", "Instant disbursement against gold valuation at competitive rates with bank-grade vault custody.", "bi-shield-lock"),
                new PublicFeatureDto("Transparent Itemized Billing", "Computerized GST invoices with clearly separated metal rates, making charges, and net weights.", "bi-receipt"),
                new PublicFeatureDto("IGI & GIA Certified Solitaires", "Handpicked natural diamonds set in hallmark 18K and 950 platinum with lifetime buyback.", "bi-gem")
            },
            categories,
            new[]
            {
                new TestimonialDto("Sunita & Rajesh Aggarwal", "We purchased our daughter's complete bridal wedding set from Shree Jewellers. The purity transparency, 6-digit HUID hallmark explanation, and warm personal care made our wedding shopping stress-free.", "Newtown, Kolkata"),
                new TestimonialDto("Ramesh Kothari", "Needed urgent working capital for my wholesale business. The gold loan verification took under 20 minutes, interest rates are crystal clear, and ornaments are in bank-grade safe custody.", "Salt Lake, Kolkata"),
                new TestimonialDto("Dr. Ananya Sen", "The 18K diamond solitaire ring and Italian Figaro chain are breathtaking. Complete GST bill, hallmarking certificate, and prompt digital billing. Unmatched trust!", "Rajarhat, Kolkata")
            },
            new[]
            {
                new QuickLinkDto("Explore Gold Loans", "/gold-loan"),
                new QuickLinkDto("Our Heritage Story", "/about"),
                new QuickLinkDto("Showroom & Contact", "/contact"),
                new QuickLinkDto("Customer Portal", "/login")
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
            new[] 
            { 
                "BIS 916 & 999 Hallmark Licensed", 
                "GST Registered & Audited Tax Compliance", 
                "Certified Diamond Assaying & Appraisals",
                "Licensed & Regulated Gold Loan Provider",
                "Lifetime Polish, Cleaning & Exchange Promise",
                "Insured Safe Vault Storage & CCTV Surveillance"
            },
            "Every gold ornament is verified for purity before display, accompanied by tamper-proof documentation and an authentic GST invoice."
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
                "Bring your gold ornaments and valid government ID proof to our showroom.",
                "Our certified gold appraisers conduct a non-destructive touchstone & electronic purity test in front of you.",
                "Loan amount is calculated up to approved LTV against current live market gold rates.",
                "Receive instant funds via Cash or direct IMPS/NEFT transfer, with sealed tamper-evident vault storage of your pledge."
            },
            new[]
            {
                "Aadhaar Card / Voter ID / Passport",
                "PAN Card (mandatory for amounts above Rs. 50,000)",
                "Active Bank Account Proof / Cancelled Cheque",
                "Passport-size photograph"
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
        var adminEmail = !string.IsNullOrWhiteSpace(settings.ShopEmail) ? settings.ShopEmail.Trim() : "chandankr.mandal87@gmail.com";

        var adminEmailTask = _notificationService.SendEmailAsync(
            adminEmail,
            $"New contact inquiry from {dto.Name}",
            $"Name: {dto.Name}\nPhone: {dto.Phone}\nEmail: {dto.Email}\n\nMessage:\n{dto.Message}");

        Task? customerEmailTask = null;
        if (!string.IsNullOrWhiteSpace(dto.Email))
        {
            customerEmailTask = _notificationService.SendEmailAsync(
                dto.Email.Trim(),
                $"Thank you for reaching out to {settings.ShopName}",
                $"Dear {dto.Name},\n\nThank you for contacting {settings.ShopName}. We have received your inquiry:\n\n\"{dto.Message}\"\n\nOur team will review your message and get in touch with you shortly at {dto.Phone} or via this email.\n\nWarm regards,\n{settings.ShopName}\nPhone: {settings.ShopPhone}\nEmail: {settings.ShopEmail}");
        }

        if (customerEmailTask != null)
        {
            await Task.WhenAll(adminEmailTask, customerEmailTask);
        }
        else
        {
            await adminEmailTask;
        }

        return Ok(new { message = "Thank you. Your message has been received successfully." });
    }

    private async Task<ShopSettingsDto> LoadSettingsAsync()
    {
        var map = await _db.AppSettings.ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        string Get(string key, string fallback) => map.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value : fallback;

        return new ShopSettingsDto(
            Get("ShopName", "Shree Jewellers"),
            Get("ShopAddress", "3B8-2D, Sanhita Township, Newtown, 700135, WB"),
            Get("GSTNumber", "19AAACS1234F1Z5"),
            Get("ShopPhone", "+91 98516 32391"),
            Get("ShopEmail", "chandankr.mandal87@gmail.com"),
            Get("LogoUrl", "/images/logo.png"),
            Get("WhatsAppNumber", "9851632391"),
            Get("MapEmbedUrl", "https://www.google.com/maps/embed?pb=!1m18!1m12!1m3!1d3683.473539876251!2d88.46876127595568!3d22.580126882226842!2m3!1f0!2f0!3f0!3m2!1i1024!2i768!4f13.1!3m3!1m2!1s0x3a02753066345e69%3A0x6b5d9852dc362545!2sNewtown%2C%20Kolkata%2C%20West%20Bengal%20700135!5e0!3m2!1sen!2sin!4v1700000000000!5m2!1sen!2sin"),
            Get("TrustedSinceYear", "1998"),
            Get("BusinessHours", "Mon - Sat: 10:30 AM - 8:30 PM | Sun: 11:00 AM - 6:00 PM"),
            Get("AboutSummary", "Led by Chandan Kr Mandal, Shree Jewellers delivers certified 22K/24K hallmarked jewellery, bridal heirlooms, fine diamond solitaires, and instant transparent gold loans with unmatched family trust."),
            Get("AboutStory", "Founded in 1998 by Chandan Kr Mandal, Shree Jewellers was established on an unshakeable promise: absolute purity, honest weight, and generational customer relationships.\n\nFrom our headquarters at 3B8-2D, Sanhita Township, Newtown, 700135, WB, we have served families across generations with handcrafted bridal necklaces, temple jewellery, and contemporary fine diamond solitaires. Every piece of gold is 100% BIS Hallmarked with a 6-digit HUID code, certified for purity, and backed by a lifetime exchange guarantee.\n\nBeyond fine jewellery, our registered Gold Loan service provides secure, compassionate liquidity with bank-grade vault custody and fast disbursement."),
            Get("OwnerName", "Chandan Kr Mandal"),
            Get("OwnerTitle", "Founder & Managing Director"),
            Get("OwnerPhotoUrl", "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=600&auto=format&fit=crop&q=80")
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
