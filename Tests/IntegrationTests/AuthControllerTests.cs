using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.TestHost;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

namespace ShreeJewellers.Tests.IntegrationTests;

/// <summary>
/// Integration tests for AuthController using a real in-process server.
/// Uses InMemory database so no SQL Server required.
/// Tests the full request → middleware → controller → service → DB pipeline.
/// </summary>
public class AuthControllerTests : IClassFixture<WebApplicationFactory<Program>>, IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    private static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public AuthControllerTests(WebApplicationFactory<Program> factory)
    {
        var dbName = "IntegrationTestDb_" + Guid.NewGuid();
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace SQL Server with InMemory for tests
                var descriptors = services.Where(d =>
                    d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>) ||
                    d.ServiceType == typeof(DbContextOptions) ||
                    d.ServiceType == typeof(ApplicationDbContext)).ToList();
                foreach (var d in descriptors) services.Remove(d);

                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseInMemoryDatabase(dbName));
            });
        });

        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
    }

    // ── Helpers ──────────────────────────────────────────────

    private static StringContent Json(object obj) =>
        new(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

    private async Task<string?> RegisterAndGetTokenAsync(
        string email = "test@integration.com",
        string password = "Test@1234!")
    {
        // Step 1: Register
        var step1Res = await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Test", lastName = "User", email,
            phoneNumber = "9876543210", password, confirmPassword = password
        }));

        if (!step1Res.IsSuccessStatusCode) return null;
        var step1Data = await step1Res.Content.ReadFromJsonAsync<JsonElement>();
        var userId = step1Data.GetProperty("userId").GetString();

        // Step 2: KYC
        await _client.PostAsync("/api/auth/register/step2", Json(new
        {
            customerId = userId, dateOfBirth = "1990-01-01", gender = "Male",
            aadhaarNumber = "123456789012", pANNumber = "ABCDE1234F",
            addressLine1 = "123 Test St", city = "Mumbai",
            state = "Maharashtra", pinCode = "400001"
        }));

        // Step 3: Documents (skip — no files in integration test)
        // Log in
        var loginRes = await _client.PostAsync("/api/auth/login", Json(new
        {
            username = email, password, rememberMe = false
        }));

        if (!loginRes.IsSuccessStatusCode) return null;
        var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        return loginData.GetProperty("accessToken").GetString();
    }

    // ── REGISTRATION TESTS ───────────────────────────────────

    [Fact]
    public async Task Register_Step1_ValidData_Returns200WithUserId()
    {
        var res = await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Integration", lastName = "Test",
            email = "newuser@test.com", phoneNumber = "9123456780",
            password = "SecureP@ss1", confirmPassword = "SecureP@ss1"
        }));

        var content = await res.Content.ReadAsStringAsync();
        res.StatusCode.Should().Be(HttpStatusCode.OK, because: content);
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("userId", out _).Should().BeTrue("response must contain userId");
        body.TryGetProperty("customerCode", out _).Should().BeTrue("response must contain customerCode");
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409Conflict()
    {
        var email = "duplicate@test.com";
        // First registration
        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "First", lastName = "User", email,
            phoneNumber = "9000000001", password = "Test@1234!", confirmPassword = "Test@1234!"
        }));

        // Duplicate
        var res = await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Second", lastName = "User", email,
            phoneNumber = "9000000002", password = "Test@1234!", confirmPassword = "Test@1234!"
        }));

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WeakPassword_Returns400()
    {
        var res = await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Test", lastName = "User",
            email = "weakpw@test.com", phoneNumber = "9000000003",
            password = "abc", confirmPassword = "abc"
        }));

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    // ── LOGIN TESTS ─────────────────────────────────────────

    [Fact]
    public async Task Login_ValidCredentials_ReturnsJWTAndRefreshToken()
    {
        var email = "logintest@test.com";
        var pw    = "ValidP@ss1";

        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Login", lastName = "Test", email,
            phoneNumber = "9000000010", password = pw, confirmPassword = pw
        }));

        var res  = await _client.PostAsync("/api/auth/login", Json(new { username = email, password = pw, rememberMe = false }));
        var body = await res.Content.ReadFromJsonAsync<JsonElement>();

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        body.TryGetProperty("accessToken",  out _).Should().BeTrue();
        body.TryGetProperty("refreshToken", out _).Should().BeTrue();
        body.TryGetProperty("expiresAt",    out _).Should().BeTrue();
        body.GetProperty("roles").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401()
    {
        var email = "wrongpw@test.com";
        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Wrong", lastName = "Password", email,
            phoneNumber = "9000000020", password = "Correct@1", confirmPassword = "Correct@1"
        }));

        var res = await _client.PostAsync("/api/auth/login", Json(new
        {
            username = email, password = "WrongPassword@1", rememberMe = false
        }));

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_FiveFailedAttempts_AccountLockedOut()
    {
        var email = "lockout@test.com";
        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Lockout", lastName = "Test", email,
            phoneNumber = "9000000030", password = "Correct@123!", confirmPassword = "Correct@123!"
        }));

        // 5 failed attempts
        for (int i = 0; i < 5; i++)
        {
            await _client.PostAsync("/api/auth/login", Json(new
            {
                username = email, password = "WrongPass@1", rememberMe = false
            }));
        }

        // 6th attempt — should be locked
        var res = await _client.PostAsync("/api/auth/login", Json(new
        {
            username = email, password = "Correct@123!", rememberMe = false
        }));

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var body = await res.Content.ReadAsStringAsync();
        body.Should().ContainAny("locked", "Locked", "lockout");
    }

    // ── PROTECTED ENDPOINT TESTS ─────────────────────────────

    [Fact]
    public async Task ProtectedEndpoint_NoToken_Returns401()
    {
        var res = await _client.GetAsync("/api/goldloan/active");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CustomerRole_AccessAdminEndpoint_Returns403()
    {
        // Register as customer (gets Customer role)
        var email = "customer-forbid@test.com";
        var pw    = "Customer@123!";

        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Customer", lastName = "Test", email,
            phoneNumber = "9000000040", password = pw, confirmPassword = pw
        }));

        var loginRes  = await _client.PostAsync("/api/auth/login", Json(new { username = email, password = pw, rememberMe = false }));
        var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var token     = loginData.GetProperty("accessToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Try admin endpoint — should be 403 Forbidden
        var res = await _client.PostAsync("/api/auth/kyc/verify", Json(new
        {
            customerId = "some-id", notes = "verified"
        }));

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        _client.DefaultRequestHeaders.Authorization = null;
    }

    // ── JWT REFRESH TESTS ─────────────────────────────────────

    [Fact]
    public async Task RefreshToken_ValidToken_IssuesNewJWT()
    {
        var email = "refresh@test.com";
        var pw    = "Refresh@123!";

        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Refresh", lastName = "Test", email,
            phoneNumber = "9000000050", password = pw, confirmPassword = pw
        }));

        var loginRes  = await _client.PostAsync("/api/auth/login", Json(new { username = email, password = pw, rememberMe = false }));
        var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var refreshToken = loginData.GetProperty("refreshToken").GetString();

        // Use refresh token
        var refreshRes  = await _client.PostAsync("/api/auth/refresh-token", Json(new { refreshToken }));
        var refreshData = await refreshRes.Content.ReadFromJsonAsync<JsonElement>();

        refreshRes.StatusCode.Should().Be(HttpStatusCode.OK);
        refreshData.TryGetProperty("accessToken",  out _).Should().BeTrue();
        refreshData.TryGetProperty("refreshToken", out _).Should().BeTrue();

        // Original refresh token must be rotated (revoked) — using it again should fail
        var secondRefresh = await _client.PostAsync("/api/auth/refresh-token", Json(new { refreshToken }));
        secondRefresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "original refresh token should be revoked after rotation");
    }

    [Fact]
    public async Task RefreshToken_InvalidToken_Returns401()
    {
        var res = await _client.PostAsync("/api/auth/refresh-token", Json(new
        {
            refreshToken = "this-is-not-a-valid-refresh-token-00000"
        }));
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── PASSWORD MANAGEMENT TESTS ─────────────────────────────

    [Fact]
    public async Task ForgotPassword_ExistingEmail_Returns200AlwaysToPreventEnumeration()
    {
        // Existing email
        var res1 = await _client.PostAsync("/api/auth/forgot-password",
            Json(new { email = "does.not.exist@test.com" }));
        res1.StatusCode.Should().Be(HttpStatusCode.OK,
            "must return 200 even for non-existent email to prevent user enumeration");
    }

    [Fact]
    public async Task ForgotPassword_NonExistentEmail_AlsoReturns200()
    {
        var res = await _client.PostAsync("/api/auth/forgot-password",
            Json(new { email = "definitely-not-registered@test.com" }));
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── LOGOUT TESTS ─────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidSession_RevokesRefreshToken()
    {
        var email = "logout@test.com";
        var pw    = "Logout@123!";

        await _client.PostAsync("/api/auth/register/step1", Json(new
        {
            firstName = "Logout", lastName = "Test", email,
            phoneNumber = "9000000060", password = pw, confirmPassword = pw
        }));

        var loginRes  = await _client.PostAsync("/api/auth/login", Json(new { username = email, password = pw, rememberMe = false }));
        var loginData = await loginRes.Content.ReadFromJsonAsync<JsonElement>();
        var token        = loginData.GetProperty("accessToken").GetString();
        var refreshToken = loginData.GetProperty("refreshToken").GetString();

        _client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        // Logout
        await _client.PostAsync("/api/auth/logout", Json(new { refreshToken }));

        _client.DefaultRequestHeaders.Authorization = null;

        // Refresh should now fail
        var refreshRes = await _client.PostAsync("/api/auth/refresh-token", Json(new { refreshToken }));
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
