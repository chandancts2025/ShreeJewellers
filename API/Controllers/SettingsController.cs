using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Portal;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;
using System.Security.Claims;

namespace ShreeJewellers.API.Controllers;

[ApiController]
[Route("api/settings")]
[Authorize(Roles = "SuperAdmin")]
[Produces("application/json")]
public class SettingsController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public SettingsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
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
