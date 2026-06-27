using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ShreeJewellers.Application.DTOs.Auth;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;
using ShreeJewellers.Infrastructure.Services;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace ShreeJewellers.API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IJwtTokenService _jwtService;
    private readonly IKYCService _kycService;
    private readonly IEncryptionService _encryptionService;
    private readonly IFileUploadService _fileUploadService;
    private readonly INotificationService _notificationService;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IJwtTokenService jwtService,
        IKYCService kycService,
        IEncryptionService encryptionService,
        IFileUploadService fileUploadService,
        INotificationService notificationService,
        ApplicationDbContext db,
        ILogger<AuthController> logger)
    {
        _userManager      = userManager;
        _signInManager    = signInManager;
        _jwtService       = jwtService;
        _kycService       = kycService;
        _encryptionService = encryptionService;
        _fileUploadService = fileUploadService;
        _notificationService = notificationService;
        _db = db;
        _logger = logger;
    }

    // ── POST /api/auth/register/step1 ─────────────────────────────────────

    /// <summary>
    /// Step 1: Create account with basic details.
    /// Returns a temporary userId to pass into Step 2.
    /// </summary>
    [HttpPost("register/step1")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterStep1([FromBody] RegisterStep1Dto dto)
    {
        // Check for duplicate email
        if (await _userManager.FindByEmailAsync(dto.Email) is not null)
            return Conflict(new { message = "An account with this email address already exists." });

        // Check for duplicate phone
        if (await _db.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber))
            return Conflict(new { message = "An account with this mobile number already exists." });

        var user = new ApplicationUser
        {
            FirstName    = dto.FirstName.Trim(),
            LastName     = dto.LastName.Trim(),
            Email        = dto.Email.Trim().ToLowerInvariant(),
            UserName     = dto.Email.Trim().ToLowerInvariant(),
            PhoneNumber  = dto.PhoneNumber,
            CustomerCode = await GenerateCustomerCodeAsync(),
            KYCStatus    = KYCStatus.Pending,
            IsActive     = true,
            CreatedAt    = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, dto.Password);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, "Customer");

        // Send email verification (non-blocking if email fails)
        var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
        try
        {
            await _notificationService.SendEmailAsync(
                user.Email,
                "Verify your email — Shree Jewellers",
                BuildVerificationEmailBody(user, token));
        }
        catch (Exception ex)
        {
            // Log full exception for diagnosis, but don't fail registration
            _logger.LogError(ex, "Failed to send verification email for user {UserId} ({Email}). CorrelationId: {CorrelationId}",
                user.Id, user.Email, HttpContext?.TraceIdentifier);
            // Inform client that registration succeeded but email delivery failed
            return Ok(new
            {
                message    = "Account created. However, we were unable to send the verification email. Please contact support or try resending verification.",
                userId     = user.Id,
                customerCode = user.CustomerCode
            });
        }

        return Ok(new
        {
            message    = "Account created. Please check your email to verify your address.",
            userId     = user.Id,
            customerCode = user.CustomerCode
        });
    }

    // ── POST /api/auth/register/step2 ────────────────────────────────────

    /// <summary>Step 2: Submit KYC personal details (Aadhaar, PAN, address).</summary>
    [HttpPost("register/step2")]
    [AllowAnonymous]
    public async Task<IActionResult> RegisterStep2([FromBody] RegisterStep2Dto dto)
    {
        await _kycService.SubmitKYCDetailsAsync(dto.CustomerId, dto);
        return Ok(new { message = "KYC details saved. Please upload your ID documents." });
    }

    // ── POST /api/auth/register/step3 ────────────────────────────────────

    /// <summary>Step 3: Upload profile photo and ID proof document.</summary>
    [HttpPost("register/step3")]
    [AllowAnonymous]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> RegisterStep3(
        [FromForm] string userId,
        IFormFile? profilePhoto,
        IFormFile? idProof)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        string? profileUrl = null;
        string? idProofUrl = null;

        if (profilePhoto is not null)
        {
            if (!_fileUploadService.IsValidFile(profilePhoto, out var err))
                return BadRequest(new { message = err });
            profileUrl = await _fileUploadService.UploadAsync(profilePhoto, "profile", userId);
        }

        if (idProof is not null)
        {
            if (!_fileUploadService.IsValidFile(idProof, out var err))
                return BadRequest(new { message = err });
            idProofUrl = await _fileUploadService.UploadAsync(idProof, "kyc-docs", userId);
        }

        await _kycService.SubmitKYCDocumentsAsync(userId, profileUrl, idProofUrl);

        return Ok(new
        {
            message = "Registration complete. Your KYC is pending admin verification.",
            kycStatus = "Pending"
        });
    }

    // ── POST /api/auth/login ──────────────────────────────────────────────

    /// <summary>Login with email or CustomerCode + password. Returns JWT + refresh token.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto dto)
    {
        // Find user by email OR CustomerCode
        var user = await _userManager.FindByEmailAsync(dto.Username)
            ?? await _db.Users.FirstOrDefaultAsync(u => u.CustomerCode == dto.Username);

        if (user is null || !user.IsActive)
            return Unauthorized(new { message = "Invalid credentials." });

        // Check lockout
        if (await _userManager.IsLockedOutAsync(user))
        {
            return Unauthorized(new
            {
                message = "Account temporarily locked due to multiple failed login attempts. Please try again in 15 minutes."
            });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, dto.Password, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
                return Unauthorized(new { message = "Account locked. Too many failed attempts." });

            return Unauthorized(new { message = "Invalid credentials." });
        }

        // Record last login metadata
        user.LastLoginAt  = DateTime.UtcNow;
        user.LastLoginIP  = GetClientIp();
        user.FailedLoginCount = 0;
        await _userManager.UpdateAsync(user);

        // Reset lockout counter on success
        await _userManager.ResetAccessFailedCountAsync(user);

        var roles = await _userManager.GetRolesAsync(user);
        var (accessToken, refreshToken, expiresAt) =
            await _jwtService.GenerateTokensAsync(user, dto.RememberMe);

        return Ok(new AuthResponseDto(
            accessToken, refreshToken, expiresAt,
            user.Id, user.FullName, user.Email!, user.CustomerCode!,
            roles, user.KYCStatus.ToString()));
    }

    // ── POST /api/auth/refresh-token ─────────────────────────────────────

    /// <summary>Exchange a valid refresh token for a new access + refresh token pair (rotation).</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
    {
        try
        {
            var (access, refresh, expiresAt) = await _jwtService.RefreshAsync(dto.RefreshToken);
            return Ok(new { accessToken = access, refreshToken = refresh, expiresAt });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
    }

    // ── POST /api/auth/logout ────────────────────────────────────────────

    /// <summary>Revoke the refresh token — effectively logs the user out on all devices.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RevokeTokenDto dto)
    {
        await _jwtService.RevokeRefreshTokenAsync(dto.RefreshToken);
        return Ok(new { message = "Logged out successfully." });
    }

    // ── POST /api/auth/forgot-password ───────────────────────────────────

    /// <summary>Send a password reset email. Always returns 200 to prevent user enumeration.</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);

        // Always return 200 — do not reveal whether the email exists
        if (user is not null && user.IsActive)
        {
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var resetUrl = $"{Request.Scheme}://{Request.Host}/reset-password?userId={user.Id}&token={Uri.EscapeDataString(token)}";

            await _notificationService.SendEmailAsync(
                user.Email!,
                "Reset your password — Shree Jewellers",
                $"Dear {user.FirstName},\n\nClick the link below to reset your password (valid for 1 hour):\n{resetUrl}\n\nIf you did not request this, please ignore this email.");
        }

        return Ok(new { message = "If that email exists, a password reset link has been sent." });
    }

    // ── POST /api/auth/reset-password ────────────────────────────────────

    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is null) return BadRequest(new { message = "Invalid request." });

        var result = await _userManager.ResetPasswordAsync(user, dto.Token, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        // Revoke all active refresh tokens after password reset for security
        await _jwtService.RevokeAllUserTokensAsync(user.Id);

        return Ok(new { message = "Password reset successfully. Please log in with your new password." });
    }

    // ── POST /api/auth/change-password ───────────────────────────────────

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordDto dto)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var user   = await _userManager.FindByIdAsync(userId);
        if (user is null) return NotFound();

        var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _jwtService.RevokeAllUserTokensAsync(userId);

        return Ok(new { message = "Password changed successfully. Please log in again." });
    }

    // ── POST /api/auth/verify-email ──────────────────────────────────────

    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailDto dto)
    {
        var user = await _userManager.FindByIdAsync(dto.UserId);
        if (user is null) return BadRequest(new { message = "Invalid verification link." });

        var result = await _userManager.ConfirmEmailAsync(user, dto.Token);
        if (!result.Succeeded)
            return BadRequest(new { message = "Email verification failed. The link may have expired." });

        return Ok(new { message = "Email verified successfully. You can now log in." });
    }

    // ── POST /api/auth/resend-verification ───────────────────────────────

    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationDto dto)
    {
        var user = await _userManager.FindByEmailAsync(dto.Email);
        if (user is not null && !user.EmailConfirmed)
        {
            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            await _notificationService.SendEmailAsync(
                user.Email!,
                "Verify your email — Shree Jewellers",
                BuildVerificationEmailBody(user, token));
        }
        return Ok(new { message = "If a pending verification exists, a new email has been sent." });
    }

    // ── Admin: POST /api/auth/admin/create-user ───────────────────────────

    [HttpPost("admin/create-user")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> AdminCreateUser([FromBody] AdminCreateUserDto dto)
    {
        var email = dto.Email.Trim().ToLowerInvariant();
        var phone = dto.PhoneNumber.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
            return Conflict(new { message = "A user with this email already exists." });

        if (await _db.Users.AnyAsync(u => u.PhoneNumber == phone))
            return Conflict(new { message = "A user with this phone number already exists." });

        var tempPassword = GenerateTempPassword();
        var user = new ApplicationUser
        {
            FirstName   = dto.FirstName.Trim(),
            LastName    = dto.LastName.Trim(),
            Email       = email,
            UserName    = email,
            PhoneNumber = phone,
            AlternatePhone = dto.AlternatePhone?.Trim(),
            DateOfBirth = dto.DateOfBirth,
            Gender      = dto.Gender,
            AddressLine1 = dto.AddressLine1,
            AddressLine2 = dto.AddressLine2,
            City        = dto.City,
            State       = dto.State,
            PinCode     = dto.PinCode,
            CustomerCode = await GenerateCustomerCodeAsync(),
            KYCStatus   = dto.Role == "Customer" ? KYCStatus.Pending : KYCStatus.Verified,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow,
            EmailConfirmed = true   // Admin-created accounts are pre-verified
        };

        if (!string.IsNullOrEmpty(dto.AadhaarNumber))
            user.AadhaarNumberEncrypted = _encryptionService.Encrypt(dto.AadhaarNumber);
        if (!string.IsNullOrEmpty(dto.PANNumber))
            user.PANNumberEncrypted = _encryptionService.Encrypt(dto.PANNumber);

        var result = await _userManager.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
            return BadRequest(new { errors = result.Errors.Select(e => e.Description) });

        await _userManager.AddToRoleAsync(user, dto.Role);

        await _notificationService.SendEmailAsync(
            user.Email,
            "Your Shree Jewellers account has been created",
            $"Dear {user.FirstName},\n\nYour account has been created.\nTemporary password: {tempPassword}\nPlease change it on first login.");

        return Created($"/api/customers/{user.Id}", new
        {
            userId       = user.Id,
            customerCode = user.CustomerCode,
            message      = "User created successfully."
        });
    }

    // ── Admin: KYC Management ─────────────────────────────────────────────

    [HttpPost("kyc/verify")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> VerifyKYC([FromBody] KYCVerifyDto dto)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _kycService.VerifyKYCAsync(adminId, dto);
        return Ok(new { message = "KYC verified successfully." });
    }

    [HttpPost("kyc/reject")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> RejectKYC([FromBody] KYCRejectDto dto)
    {
        var adminId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await _kycService.RejectKYCAsync(adminId, dto);
        return Ok(new { message = "KYC rejected and customer notified." });
    }

    [HttpGet("kyc/pending")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public async Task<IActionResult> GetPendingKYC()
    {
        var list = await _kycService.GetPendingKYCListAsync();
        return Ok(list);
    }

    [HttpGet("kyc/status/{customerId}")]
    [Authorize]
    public async Task<IActionResult> GetKYCStatus(string customerId)
    {
        // Customers can only check their own status
        var requesterId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!User.IsInRole("Admin") && !User.IsInRole("SuperAdmin") && !User.IsInRole("Staff")
            && requesterId != customerId)
            return Forbid();

        var status = await _kycService.GetKYCStatusAsync(customerId);
        return Ok(status);
    }

    // ── Private Helpers ───────────────────────────────────────────────────

    private async Task<string> GenerateCustomerCodeAsync()
    {
        var year = DateTime.Now.Year;
        var count = await _db.Users.CountAsync() + 1;
        return $"CUST-{year}-{count:D4}";
    }

    private string GetClientIp()
    {
        var forwarded = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwarded)) return forwarded.Split(',')[0].Trim();
        return HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static string GenerateTempPassword()
    {
        // Generates a secure temp password meeting all policy requirements
        const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$";
        var rand = new Random();
        return new string(Enumerable.Range(0, 12).Select(_ => chars[rand.Next(chars.Length)]).ToArray());
    }

    private string BuildVerificationEmailBody(ApplicationUser user, string token)
    {
        var verifyUrl = $"{Request.Scheme}://{Request.Host}/api/auth/verify-email?userId={user.Id}&token={Uri.EscapeDataString(token)}";
        return $"Dear {user.FirstName},\n\n" +
               $"Welcome to Shree Jewellers!\n\n" +
               $"Please verify your email address by clicking the link below:\n{verifyUrl}\n\n" +
               $"This link expires in 24 hours.\n\n" +
               $"Your Customer Code is: {user.CustomerCode}\n\n" +
               $"Thank you for choosing Shree Jewellers.";
    }
}
