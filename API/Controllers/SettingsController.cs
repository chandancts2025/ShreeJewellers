using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Portal;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;
using System.Security.Claims;

using ShreeJewellers.Infrastructure.Services;

namespace ShreeJewellers.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "SuperAdmin,Admin")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INotificationService _notificationService;
    private readonly IConfiguration _config;

    public SettingsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        INotificationService notificationService,
        IConfiguration config)
    {
        _db = db;
        _userManager = userManager;
        _notificationService = notificationService;
        _config = config;
    }

    [HttpGet("shop")]
    public async Task<IActionResult> GetShopSettings()
    {
        var map = await _db.AppSettings.ToDictionaryAsync(x => x.SettingKey, x => x.SettingValue);
        string Get(string key, string fallback) => map.TryGetValue(key, out var value) ? value : fallback;

        return Ok(new ShopSettingsDto(
            Get("ShopName", "Shree Jewellers"),
            Get("ShopAddress", ""),
            Get("GSTNumber", ""),
            Get("ShopPhone", ""),
            Get("ShopEmail", ""),
            Get("LogoUrl", ""),
            Get("WhatsAppNumber", ""),
            Get("MapEmbedUrl", ""),
            Get("TrustedSinceYear", "1998"),
            Get("BusinessHours", ""),
            Get("AboutSummary", ""),
            Get("AboutStory", ""),
            Get("OwnerName", ""),
            Get("OwnerTitle", ""),
            Get("OwnerPhotoUrl", "")
        ));
    }

    [HttpPut("shop")]
    public async Task<IActionResult> UpdateShopSettings([FromBody] UpdateShopSettingsDto dto)
    {
        await UpsertSettingAsync("ShopName", dto.ShopName);
        await UpsertSettingAsync("ShopAddress", dto.ShopAddress);
        await UpsertSettingAsync("GSTNumber", dto.GSTNumber);
        await UpsertSettingAsync("ShopPhone", dto.ShopPhone);
        await UpsertSettingAsync("ShopEmail", dto.ShopEmail);
        await UpsertSettingAsync("LogoUrl", dto.LogoUrl);
        await UpsertSettingAsync("WhatsAppNumber", dto.WhatsAppNumber);
        await UpsertSettingAsync("MapEmbedUrl", dto.MapEmbedUrl);
        await UpsertSettingAsync("TrustedSinceYear", dto.TrustedSinceYear);
        await UpsertSettingAsync("BusinessHours", dto.BusinessHours);
        await UpsertSettingAsync("AboutSummary", dto.AboutSummary);
        await UpsertSettingAsync("AboutStory", dto.AboutStory);
        await UpsertSettingAsync("OwnerName", dto.OwnerName);
        await UpsertSettingAsync("OwnerTitle", dto.OwnerTitle);
        await UpsertSettingAsync("OwnerPhotoUrl", dto.OwnerPhotoUrl);

        await _db.SaveChangesAsync();
        return Ok(new { message = "Shop settings updated successfully." });
    }

    [HttpGet("interest-rates")]
    public async Task<IActionResult> GetInterestRates()
    {
        var items = await _db.InterestSettings
            .OrderByDescending(x => x.EffectiveFrom)
            .Select(x => new InterestSettingListItemDto(
                x.Id,
                x.InterestRatePercent,
                x.CalculationType,
                x.CompoundingEnabled,
                x.PenaltyRatePercent,
                x.DefaultTenureMonths,
                x.LoanToValuePercent,
                x.EffectiveFrom,
                x.EffectiveTo))
            .ToListAsync();

        return Ok(items);
    }

    [HttpPost("interest-rates")]
    public async Task<IActionResult> CreateInterestRate([FromBody] UpsertInterestSettingDto dto)
    {
        var currentActive = await _db.InterestSettings.Where(x => x.EffectiveTo == null).ToListAsync();
        foreach (var item in currentActive)
            item.EffectiveTo = dto.EffectiveFrom.AddDays(-1);

        _db.InterestSettings.Add(new InterestSetting
        {
            InterestRatePercent = dto.InterestRatePercent,
            CalculationType = dto.CalculationType,
            CompoundingEnabled = dto.CompoundingEnabled,
            PenaltyRatePercent = dto.PenaltyRatePercent,
            DefaultTenureMonths = dto.DefaultTenureMonths,
            LoanToValuePercent = dto.LoanToValuePercent,
            EffectiveFrom = dto.EffectiveFrom,
            CreatedAt = DateTime.UtcNow,
            CreatedByUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "SYSTEM"
        });

        await _db.SaveChangesAsync();
        return Ok(new { message = "Interest setting added successfully." });
    }

    [HttpGet("users")]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users.OrderByDescending(x => x.CreatedAt).ToListAsync();
        var result = new List<CustomerListItemDto>();
        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            result.Add(new CustomerListItemDto(
                user.Id,
                user.FullName,
                user.Email ?? string.Empty,
                user.PhoneNumber ?? string.Empty,
                user.CustomerCode,
                user.KYCStatus.ToString(),
                user.IsActive,
                user.CreatedAt,
                string.Join(", ", roles)));
        }

        return Ok(result);
    }

    [HttpGet("gold-price")]
    public async Task<IActionResult> GetGoldPriceSettings()
    {
        var refresh = await _db.AppSettings
            .Where(x => x.SettingKey == "GoldPriceRefreshIntervalMinutes")
            .Select(x => x.SettingValue)
            .FirstOrDefaultAsync() ?? "15";

        return Ok(new GoldPriceSettingsDto("Stored securely in configuration.", refresh, "Optional manual override reason"));
    }

    [HttpPut("gold-price")]
    public async Task<IActionResult> UpdateGoldPriceSettings([FromBody] UpdateGoldPriceSettingsDto dto)
    {
        await UpsertSettingAsync("GoldPriceRefreshIntervalMinutes", dto.RefreshIntervalMinutes);
        await UpsertSettingAsync("GoldPriceApiKeyHint", string.IsNullOrWhiteSpace(dto.ApiKey) ? "Not updated" : "Configured");
        await _db.SaveChangesAsync();
        return Ok(new { message = "Gold price settings saved successfully." });
    }

    [HttpGet("email")]
    public IActionResult GetEmailSettings()
    {
        var smtpConfig = _config.GetSection("SmtpSettings");
        var emailConfig = _config.GetSection("EmailSettings");

        var host = smtpConfig["Host"] ?? emailConfig["SmtpHost"] ?? "smtp.gmail.com";
        var port = smtpConfig["Port"] ?? emailConfig["SmtpPort"] ?? "587";
        var enableSsl = bool.TryParse(smtpConfig["EnableSsl"] ?? emailConfig["EnableSsl"], out var ssl) ? ssl : true;
        var userName = smtpConfig["UserName"] ?? emailConfig["SenderEmail"] ?? smtpConfig["FromEmail"] ?? "";
        var fromEmail = smtpConfig["FromEmail"] ?? emailConfig["SenderEmail"] ?? userName;
        var fromName = smtpConfig["FromName"] ?? emailConfig["SenderName"] ?? "EduManage Pro Academy";
        var devMode = bool.TryParse(smtpConfig["DevMode"] ?? emailConfig["DevMode"], out var dev) && dev;
        var password = smtpConfig["Password"] ?? emailConfig["SenderPassword"];
        var isConfigured = !string.IsNullOrWhiteSpace(password) && !password.StartsWith("SET_IN_ENV", StringComparison.OrdinalIgnoreCase);

        return Ok(new EmailSettingsDto(
            host,
            port,
            enableSsl,
            fromEmail,
            fromName,
            devMode,
            isConfigured
        ));
    }

    [HttpPost("test-email")]
    public async Task<IActionResult> SendTestEmail([FromBody] TestEmailRequestDto? dto)
    {
        var recipient = !string.IsNullOrWhiteSpace(dto?.RecipientEmail)
            ? dto.RecipientEmail.Trim()
            : (User.FindFirstValue(ClaimTypes.Email) ?? "chandankr.mandal87@gmail.com");

        var result = await _notificationService.SendEmailWithStatusAsync(
            recipient,
            "Verification Test: Shree Jewellers Email Service",
            $"<h3>Email Service Working</h3><p>Your SMTP mail configuration is properly connected and functioning.</p><p>Recipient: {recipient}</p><p>Server Time: {DateTime.UtcNow:u}</p>",
            isHtml: true);

        if (result.Success)
            return Ok(new { success = true, message = result.Message });

        return BadRequest(new { success = false, message = result.Message });
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await _db.AppSettings.FirstOrDefaultAsync(x => x.SettingKey == key);
        if (setting is null)
        {
            _db.AppSettings.Add(new AppSetting
            {
                SettingKey = key,
                SettingValue = value,
                UpdatedAt = DateTime.UtcNow
            });
            return;
        }

        setting.SettingValue = value;
        setting.UpdatedAt = DateTime.UtcNow;
    }
}
