using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShreeJewellers.Application.DTOs.Auth;
using ShreeJewellers.Domain.Entities;
using ShreeJewellers.Domain.Enums;
using ShreeJewellers.Infrastructure.Data;

namespace ShreeJewellers.Infrastructure.Services;

public interface IKYCService
{
    Task SubmitKYCDetailsAsync(string userId, RegisterStep2Dto dto);
    Task SubmitKYCDocumentsAsync(string userId, string? profilePhotoUrl, string? idProofUrl);
    Task VerifyKYCAsync(string adminUserId, KYCVerifyDto dto);
    Task RejectKYCAsync(string adminUserId, KYCRejectDto dto);
    Task<KYCStatusResponseDto> GetKYCStatusAsync(string customerId);
    Task<IEnumerable<KYCStatusResponseDto>> GetPendingKYCListAsync();
}

public class KYCService : IKYCService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEncryptionService _encryptionService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<KYCService> _logger;

    public KYCService(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IEncryptionService encryptionService,
        INotificationService notificationService,
        ILogger<KYCService> logger)
    {
        _db = db;
        _userManager = userManager;
        _encryptionService = encryptionService;
        _notificationService = notificationService;
        _logger = logger;
    }

    // ── Submit KYC Personal Details ───────────────────────────────────────

    public async Task SubmitKYCDetailsAsync(string userId, RegisterStep2Dto dto)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        // Encrypt sensitive identifiers before persisting
        user.AadhaarNumberEncrypted = _encryptionService.Encrypt(dto.AadhaarNumber);
        user.PANNumberEncrypted     = _encryptionService.Encrypt(dto.PANNumber);

        user.DateOfBirth   = dto.DateOfBirth;
        user.Gender        = dto.Gender;
        user.AddressLine1  = dto.AddressLine1;
        user.AddressLine2  = dto.AddressLine2;
        user.City          = dto.City;
        user.State         = dto.State;
        user.PinCode       = dto.PinCode;
        user.AlternatePhone = dto.AlternatePhone;
        user.KYCStatus     = KYCStatus.Pending;
        user.UpdatedAt     = DateTime.UtcNow;

        // Handle referral
        if (!string.IsNullOrEmpty(dto.ReferralCode))
        {
            var referrer = await _db.Users.FirstOrDefaultAsync(u => u.CustomerCode == dto.ReferralCode);
            if (referrer is not null)
                user.ReferredByUserId = referrer.Id;
        }

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException(
                "KYC details update failed: " + string.Join(", ", result.Errors.Select(e => e.Description)));

        _logger.LogInformation("KYC details submitted for user {UserId}", userId);

        // Notify admin team about new KYC submission
        await _notificationService.NotifyAdminsAsync(
            subject: "New KYC Submission",
            body: $"Customer {user.FullName} ({user.CustomerCode}) has submitted KYC documents for verification.");
    }

    // ── Submit KYC Documents ──────────────────────────────────────────────

    public async Task SubmitKYCDocumentsAsync(string userId, string? profilePhotoUrl, string? idProofUrl)
    {
        var user = await _userManager.FindByIdAsync(userId)
            ?? throw new KeyNotFoundException("User not found.");

        if (!string.IsNullOrEmpty(profilePhotoUrl))
            user.ProfilePhotoUrl = profilePhotoUrl;

        if (!string.IsNullOrEmpty(idProofUrl))
            user.IDProofUrl = idProofUrl;

        user.UpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);
        _logger.LogInformation("KYC documents uploaded for user {UserId}", userId);
    }

    // ── Admin: Verify KYC ─────────────────────────────────────────────────

    public async Task VerifyKYCAsync(string adminUserId, KYCVerifyDto dto)
    {
        var customer = await _db.Users.FindAsync(dto.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        if (customer.KYCStatus == KYCStatus.Verified)
            throw new InvalidOperationException("KYC is already verified for this customer.");

        var previousStatus = customer.KYCStatus;

        customer.KYCStatus          = KYCStatus.Verified;
        customer.KYCVerifiedAt       = DateTime.UtcNow;
        customer.KYCVerifiedByUserId = adminUserId;
        customer.KYCRejectionReason  = null;
        customer.UpdatedAt           = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _logger.LogInformation(
            "KYC verified for customer {CustomerId} by admin {AdminId}",
            dto.CustomerId, adminUserId);

        // Audit log
        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = adminUserId,
            Action     = "KYC_VERIFY",
            EntityName = "ApplicationUser",
            EntityId   = dto.CustomerId,
            OldValues  = $"{{\"KYCStatus\":\"{previousStatus}\"}}",
            NewValues  = $"{{\"KYCStatus\":\"Verified\",\"Notes\":\"{dto.Notes}\"}}",
            Timestamp  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        // Notify customer
        await _notificationService.SendEmailAsync(
            customer.Email!,
            "KYC Verified — Shree Jewellers",
            $"Dear {customer.FirstName},\n\nYour KYC verification is complete. " +
            $"You can now access all Gold Loan services.\n\nThank you for choosing Shree Jewellers.");
    }

    // ── Admin: Reject KYC ────────────────────────────────────────────────

    public async Task RejectKYCAsync(string adminUserId, KYCRejectDto dto)
    {
        var customer = await _db.Users.FindAsync(dto.CustomerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        var previousStatus = customer.KYCStatus;

        customer.KYCStatus           = KYCStatus.Rejected;
        customer.KYCRejectionReason  = dto.RejectionReason;
        customer.KYCVerifiedByUserId = adminUserId;
        customer.UpdatedAt           = DateTime.UtcNow;

        await _db.SaveChangesAsync();

        _db.AuditLogs.Add(new AuditLog
        {
            UserId     = adminUserId,
            Action     = "KYC_REJECT",
            EntityName = "ApplicationUser",
            EntityId   = dto.CustomerId,
            OldValues  = $"{{\"KYCStatus\":\"{previousStatus}\"}}",
            NewValues  = $"{{\"KYCStatus\":\"Rejected\",\"Reason\":\"{dto.RejectionReason}\"}}",
            Timestamp  = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();

        _logger.LogWarning("KYC rejected for {CustomerId}: {Reason}", dto.CustomerId, dto.RejectionReason);

        await _notificationService.SendEmailAsync(
            customer.Email!,
            "KYC Verification Update — Shree Jewellers",
            $"Dear {customer.FirstName},\n\nWe were unable to verify your KYC documents.\n" +
            $"Reason: {dto.RejectionReason}\n\nPlease visit the shop or re-upload your documents.");
    }

    // ── Query KYC Status ─────────────────────────────────────────────────

    public async Task<KYCStatusResponseDto> GetKYCStatusAsync(string customerId)
    {
        var user = await _db.Users.FindAsync(customerId)
            ?? throw new KeyNotFoundException("Customer not found.");

        return MapToKycResponse(user);
    }

    public async Task<IEnumerable<KYCStatusResponseDto>> GetPendingKYCListAsync()
    {
        var users = await _db.Users
            .Where(u => u.KYCStatus == KYCStatus.Pending && u.IsActive)
            .OrderBy(u => u.CreatedAt)
            .ToListAsync();

        return users.Select(MapToKycResponse);
    }

    // ── Private ───────────────────────────────────────────────────────────

    private static KYCStatusResponseDto MapToKycResponse(ApplicationUser u) => new(
        u.Id,
        u.CustomerCode ?? string.Empty,
        u.FullName,
        u.KYCStatus.ToString(),
        u.KYCVerifiedAt,
        u.KYCRejectionReason);
}
