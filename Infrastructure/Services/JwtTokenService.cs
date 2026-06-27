using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Infrastructure.Data;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace ShreeJewellers.Infrastructure.Services;

public interface IJwtTokenService
{
    Task<(string accessToken, string refreshToken, DateTime expiresAt)> GenerateTokensAsync(
        ApplicationUser user, bool rememberMe = false);

    Task<ClaimsPrincipal?> ValidateExpiredTokenAsync(string accessToken);
    Task<(string newAccessToken, string newRefreshToken, DateTime expiresAt)> RefreshAsync(string refreshToken);
    Task RevokeRefreshTokenAsync(string refreshToken);
    Task RevokeAllUserTokensAsync(string userId);
}

/// <summary>
/// Generates short-lived JWT access tokens (15 min) and long-lived refresh tokens
/// (7 days standard, 30 days with rememberMe). Refresh tokens are stored hashed
/// in UserTokens (Identity) so they survive restarts and can be revoked server-side.
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    // Config keys
    private const string TokenProvider = "ShreeJewellers";
    private const string RefreshTokenName = "RefreshToken";

    public JwtTokenService(
        IConfiguration config,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext db)
    {
        _config = config;
        _userManager = userManager;
        _db = db;
    }

    // ── Generate Tokens ───────────────────────────────────────────────────

    public async Task<(string accessToken, string refreshToken, DateTime expiresAt)>
        GenerateTokensAsync(ApplicationUser user, bool rememberMe = false)
    {
        var roles = await _userManager.GetRolesAsync(user);
        var accessToken = BuildAccessToken(user, roles);
        var (rawRefreshToken, expiresAt) = await StoreRefreshTokenAsync(user, rememberMe);

        return (accessToken, rawRefreshToken, expiresAt);
    }

    // ── Build JWT Access Token ────────────────────────────────────────────

    private string BuildAccessToken(ApplicationUser user, IList<string> roles)
    {
        var jwtConfig = _config.GetSection("JwtSettings");
        var secretKey = jwtConfig["SecretKey"]
            ?? throw new InvalidOperationException("JWT SecretKey is not configured.");

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var expiryMinutes = int.Parse(jwtConfig["AccessTokenExpiryMinutes"] ?? "15");

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
                ClaimValueTypes.Integer64),
            new("customerCode", user.CustomerCode ?? string.Empty),
            new("fullName", $"{user.FirstName} {user.LastName}"),
            new("kycStatus", user.KYCStatus.ToString()),
        };

        // Add one claim per role (standard pattern for ASP.NET Core role checks)
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var token = new JwtSecurityToken(
            issuer: jwtConfig["Issuer"],
            audience: jwtConfig["Audience"],
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // ── Store Refresh Token (hashed) via Identity UserTokens ─────────────

    private async Task<(string rawToken, DateTime expiresAt)> StoreRefreshTokenAsync(
        ApplicationUser user, bool rememberMe)
    {
        var jwtConfig = _config.GetSection("JwtSettings");
        var days = rememberMe
            ? int.Parse(jwtConfig["RememberMeRefreshTokenDays"] ?? "30")
            : int.Parse(jwtConfig["RefreshTokenExpiryDays"] ?? "7");

        var expiresAt = DateTime.UtcNow.AddDays(days);

        // Generate a cryptographically secure raw token
        var rawToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));

        // Encode expiry into stored value: "hash|expiry"
        var hash = HashToken(rawToken);
        var storedValue = $"{hash}|{expiresAt:O}";

        // Identity's SetAuthenticationTokenAsync replaces any existing token for this login provider
        await _userManager.SetAuthenticationTokenAsync(
            user, TokenProvider, RefreshTokenName, storedValue);

        return (rawToken, expiresAt);
    }

    // ── Refresh ───────────────────────────────────────────────────────────

    public async Task<(string newAccessToken, string newRefreshToken, DateTime expiresAt)>
        RefreshAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);

        // Search all users' tokens for a matching hash
        var token = await _db.UserTokens
            .Where(t => t.LoginProvider == TokenProvider && t.Name == RefreshTokenName)
            .FirstOrDefaultAsync(t => t.Value != null && t.Value.StartsWith(hash + "|"));

        if (token is null)
            throw new UnauthorizedAccessException("Invalid refresh token.");

        // Parse stored value
        var parts = token.Value!.Split('|');
        if (parts.Length != 2 || !DateTime.TryParse(parts[1], out var expiry))
            throw new UnauthorizedAccessException("Malformed refresh token.");

        if (expiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token has expired. Please log in again.");

        var user = await _userManager.FindByIdAsync(token.UserId);
        if (user is null || !user.IsActive)
            throw new UnauthorizedAccessException("User account not found or inactive.");

        // Rotate: revoke old, issue new (prevents replay attacks)
        await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
        return await GenerateTokensAsync(user);
    }

    // ── Revoke ────────────────────────────────────────────────────────────

    public async Task RevokeRefreshTokenAsync(string refreshToken)
    {
        var hash = HashToken(refreshToken);
        var token = await _db.UserTokens
            .FirstOrDefaultAsync(t =>
                t.LoginProvider == TokenProvider &&
                t.Name == RefreshTokenName &&
                t.Value != null && t.Value.StartsWith(hash + "|"));

        if (token is not null)
        {
            var user = await _userManager.FindByIdAsync(token.UserId);
            if (user is not null)
                await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
        }
    }

    public async Task RevokeAllUserTokensAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is not null)
            await _userManager.RemoveAuthenticationTokenAsync(user, TokenProvider, RefreshTokenName);
    }

    // ── Validate Expired Token (for refresh flow) ─────────────────────────

    public Task<ClaimsPrincipal?> ValidateExpiredTokenAsync(string accessToken)
    {
        var jwtConfig = _config.GetSection("JwtSettings");
        var secretKey = jwtConfig["SecretKey"]!;

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtConfig["Issuer"],
            ValidateAudience = true,
            ValidAudience = jwtConfig["Audience"],
            ValidateLifetime = false,   // Allow expired tokens in refresh flow
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = new JwtSecurityTokenHandler()
                .ValidateToken(accessToken, parameters, out _);
            return Task.FromResult<ClaimsPrincipal?>(principal);
        }
        catch
        {
            return Task.FromResult<ClaimsPrincipal?>(null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    /// <summary>SHA-256 hash of the raw refresh token for safe storage.</summary>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
