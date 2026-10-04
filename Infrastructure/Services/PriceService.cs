using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ShreeJewelers.Application.DTOs;
using ShreeJewellers.Application.DTOs;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShreeJewellers.Infrastructure.Services;

// ── DTO (was in stub, keep here for compilation) ──────────────────────────────
public record GoldPriceDto(
    decimal Rate22KPer10g,
    decimal Rate24KPer10g,
    decimal SilverRatePerKg,
    DateTime RecordedAt,
    bool IsManualOverride
);

public interface IPriceService
{
    Task<GoldPriceDto> GetCurrentRateAsync();
    Task<GoldPriceDto?> GetRateForDateAsync(DateTime date);
    Task SetManualOverrideAsync(ManualPriceOverrideDto dto, string adminUserId);
    Task<List<GoldPriceResponseDto>> GetPriceHistoryAsync(DateTime from, DateTime to);
}

/// <summary>
/// Fetches live gold/silver prices from GoldAPI.io with a 15-minute IMemoryCache.
/// Falls back to last-known price from GoldPriceHistory table if the API is unavailable.
/// Admin can set a manual override for the current trading day.
/// </summary>
public class PriceService : IPriceService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<PriceService> _logger;

    private const string CacheKey         = "GOLD_PRICE_CURRENT";
    private const string ManualCacheKey   = "GOLD_PRICE_MANUAL_OVERRIDE";
    private const string FallbackCacheKey = "GOLD_PRICE_FALLBACK";
    private const decimal GramsPerOunce   = 31.1035m;

    public PriceService(IHttpClientFactory httpClientFactory, IMemoryCache cache,
        ApplicationDbContext db, IConfiguration config, ILogger<PriceService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache; _db = db; _config = config; _logger = logger;
    }

    // ── Get Current Rate ──────────────────────────────────────────────────

    public async Task<GoldPriceDto> GetCurrentRateAsync()
    {
        // 1. Manual override takes highest priority
        if (_cache.TryGetValue(ManualCacheKey, out GoldPriceDto? manual) && manual is not null)
            return manual;

        var todayOverride = await _db.GoldPriceHistory
            .Where(p => p.IsManualOverride && p.RecordedAt.Date == DateTime.UtcNow.Date && p.GoldRatePer10Gram_24K > 0)
            .OrderByDescending(p => p.RecordedAt).FirstOrDefaultAsync();

        if (todayOverride is not null)
        {
            var m = MapToDto(todayOverride, true);
            _cache.Set(ManualCacheKey, m, TimeSpan.FromHours(4));
            return m;
        }

        // 2. Regular cache (15 min)
        if (_cache.TryGetValue(CacheKey, out GoldPriceDto? cached) && cached is not null && cached.Rate24KPer10g > 0)
            return cached;

        // 3. Live API
        try
        {
            var price   = await FetchFromApiAsync();
            var minutes = int.Parse(_config["GoldPriceApi:RefreshIntervalMinutes"] ?? "15");
            _cache.Set(CacheKey, price, TimeSpan.FromMinutes(minutes));
            _cache.Set(FallbackCacheKey, price, TimeSpan.FromHours(24));
            await SavePriceHistoryAsync(price);
            return price;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GoldAPI.io unavailable or unconfigured — using fallback.");
            return await GetFallbackPriceAsync();
        }
    }

    // ── GoldAPI.io fetch ──────────────────────────────────────────────────

    private async Task<GoldPriceDto> FetchFromApiAsync()
    {
        var apiKey = _config["GoldPriceApi:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey) || apiKey.StartsWith("SET_IN_ENV"))
            throw new InvalidOperationException("GoldAPI key not configured.");

        var client   = _httpClientFactory.CreateClient("GoldAPI");
        var currency = _config["GoldPriceApi:Currency"] ?? "INR";
        var opts     = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

        var goldResp = await client.GetAsync($"XAU/{currency}");
        if (!goldResp.IsSuccessStatusCode)
            throw new HttpRequestException($"Gold API returned {goldResp.StatusCode}");

        var goldJson = await goldResp.Content.ReadAsStringAsync();
        var goldData = JsonSerializer.Deserialize<GoldApiResponse>(goldJson, opts);
        if (goldData == null || goldData.Price <= 0)
            throw new InvalidOperationException("Gold API returned invalid or zero gold price.");

        decimal silverKg = 0m;
        try
        {
            var silverResp = await client.GetAsync($"XAG/{currency}");
            if (silverResp.IsSuccessStatusCode)
            {
                var silverJson = await silverResp.Content.ReadAsStringAsync();
                var silverData = JsonSerializer.Deserialize<GoldApiResponse>(silverJson, opts);
                if (silverData != null && silverData.Price > 0)
                {
                    silverKg = Math.Round((silverData.Price / GramsPerOunce) * 1000, 2);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch silver price from GoldAPI.");
        }

        // troy ounce → per 10g
        var rate24K   = Math.Round((goldData.Price / GramsPerOunce) * 10, 2);
        var rate22K   = Math.Round(rate24K * (22m / 24m), 2);
        if (silverKg <= 0) silverKg = 88000m;

        return new GoldPriceDto(rate22K, rate24K, silverKg, DateTime.UtcNow, false);
    }

    // ── Fallback ──────────────────────────────────────────────────────────

    private async Task<GoldPriceDto> GetFallbackPriceAsync()
    {
        if (_cache.TryGetValue(FallbackCacheKey, out GoldPriceDto? fb) && fb is not null && fb.Rate24KPer10g > 0)
            return fb;

        var last = await _db.GoldPriceHistory
            .Where(p => p.GoldRatePer10Gram_24K > 0 && p.GoldRatePer10Gram_22K > 0)
            .OrderByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();

        if (last is not null)
        {
            var dto = MapToDto(last, last.IsManualOverride);
            _cache.Set(FallbackCacheKey, dto, TimeSpan.FromHours(24));
            return dto;
        }

        _logger.LogWarning("No positive gold price available in history — using market baseline rates.");
        return new GoldPriceDto(66_460m, 72_500m, 88_000m, DateTime.UtcNow, false);
    }

    // ── Manual Override ───────────────────────────────────────────────────

    public async Task SetManualOverrideAsync(ManualPriceOverrideDto dto, string adminUserId)
    {
        var current = await GetCurrentRateAsync();
        var entry = new GoldPriceHistory
        {
            RecordedAt            = DateTime.UtcNow,
            GoldRatePer10Gram_22K = dto.GoldRate22KPer10g ?? current.Rate22KPer10g,
            GoldRatePer10Gram_24K = dto.GoldRate24KPer10g ?? current.Rate24KPer10g,
            SilverRatePerKg       = dto.SilverRatePerKg ?? current.SilverRatePerKg,
            Source                = PriceSource.Manual,
            IsManualOverride      = true,
            CreatedByUserId       = adminUserId
        };
        _db.GoldPriceHistory.Add(entry);
        await _db.SaveChangesAsync();

        var overrideDto = new GoldPriceDto(entry.GoldRatePer10Gram_22K,
            entry.GoldRatePer10Gram_24K, entry.SilverRatePerKg, DateTime.UtcNow, true);

        _cache.Set(ManualCacheKey, overrideDto, DateTime.Today.AddDays(1) - DateTime.UtcNow);
        _logger.LogInformation("Manual price override: 22K={Rate22K}, 24K={Rate24K}",
            entry.GoldRatePer10Gram_22K, entry.GoldRatePer10Gram_24K);
    }

    // ── History ───────────────────────────────────────────────────────────

    public async Task<GoldPriceDto?> GetRateForDateAsync(DateTime date)
    {
        var r = await _db.GoldPriceHistory
            .Where(p => p.RecordedAt.Date == date.Date)
            .OrderByDescending(p => p.IsManualOverride).ThenByDescending(p => p.RecordedAt)
            .FirstOrDefaultAsync();
        return r is null ? null : MapToDto(r, r.IsManualOverride);
    }

    public async Task<List<GoldPriceResponseDto>> GetPriceHistoryAsync(DateTime from, DateTime to)
        => await _db.GoldPriceHistory
            .Where(p => p.RecordedAt >= from && p.RecordedAt <= to)
            .OrderByDescending(p => p.RecordedAt)
            .Select(p => new GoldPriceResponseDto(
                p.GoldRatePer10Gram_22K, p.GoldRatePer10Gram_24K, p.SilverRatePerKg,
                Math.Round(p.GoldRatePer10Gram_22K / 10, 2),
                Math.Round(p.GoldRatePer10Gram_24K / 10, 2),
                Math.Round(p.SilverRatePerKg / 1000, 2),
                p.RecordedAt, p.IsManualOverride, p.Source.ToString()))
            .ToListAsync();

    // ── Helpers ───────────────────────────────────────────────────────────

    private async Task SavePriceHistoryAsync(GoldPriceDto dto)
    {
        if (dto.Rate24KPer10g <= 0 || dto.Rate22KPer10g <= 0) return;

        _db.GoldPriceHistory.Add(new GoldPriceHistory
        {
            RecordedAt            = dto.RecordedAt,
            GoldRatePer10Gram_22K = dto.Rate22KPer10g,
            GoldRatePer10Gram_24K = dto.Rate24KPer10g,
            SilverRatePerKg       = dto.SilverRatePerKg,
            Source                = PriceSource.API
        });
        await _db.SaveChangesAsync();
    }

    private static GoldPriceDto MapToDto(GoldPriceHistory h, bool isOverride)
        => new(h.GoldRatePer10Gram_22K, h.GoldRatePer10Gram_24K,
               h.SilverRatePerKg, h.RecordedAt, isOverride);

    private record GoldApiResponse(
        [property: JsonPropertyName("price")]    decimal Price,
        [property: JsonPropertyName("currency")] string Currency);
}
