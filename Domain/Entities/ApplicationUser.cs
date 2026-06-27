using Microsoft.AspNetCore.Identity;
using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Extends ASP.NET Core IdentityUser with full KYC and shop-specific fields.
/// Aadhaar and PAN are stored AES-256 encrypted — never in plaintext.
/// </summary>
public class ApplicationUser : IdentityUser, IAuditableEntity
{
    // ── Personal Details ──────────────────────────────────────────────────

    [Required]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [NotMapped]
    public string FullName => $"{FirstName} {LastName}";

    [Required]
    public DateOnly DateOfBirth { get; set; }

    [Required]
    [MaxLength(20)]
    public string Gender { get; set; } = string.Empty;  // Male / Female / Other

    [MaxLength(500)]
    public string? ProfilePhotoUrl { get; set; }

    [MaxLength(500)]
    public string? IDProofUrl { get; set; }  // Uploaded Aadhaar/PAN scan

    // ── KYC Sensitive Data (AES-256 encrypted at application layer) ───────

    /// <summary>AES-256-CBC encrypted Aadhaar number. Never return to Customer role.</summary>
    [MaxLength(500)]
    public string? AadhaarNumberEncrypted { get; set; }

    /// <summary>AES-256-CBC encrypted PAN number. Never return to Customer role.</summary>
    [MaxLength(500)]
    public string? PANNumberEncrypted { get; set; }

    // ── Address ───────────────────────────────────────────────────────────

    [Required]
    [MaxLength(200)]
    public string AddressLine1 { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? AddressLine2 { get; set; }

    [Required]
    [MaxLength(100)]
    public string City { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string State { get; set; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string PinCode { get; set; } = string.Empty;

    [MaxLength(15)]
    public string? AlternatePhone { get; set; }

    // ── Shop-Specific Fields ──────────────────────────────────────────────

    /// <summary>Auto-generated unique code: CUST-2024-0001</summary>
    [MaxLength(20)]
    public string? CustomerCode { get; set; }

    public KYCStatus KYCStatus { get; set; } = KYCStatus.Pending;

    public DateTime? KYCVerifiedAt { get; set; }

    [MaxLength(450)]
    public string? KYCVerifiedByUserId { get; set; }

    [MaxLength(500)]
    public string? KYCRejectionReason { get; set; }

    // Referral system — self-referencing FK
    [MaxLength(450)]
    public string? ReferredByUserId { get; set; }

    public int LoyaltyPoints { get; set; } = 0;

    public bool IsActive { get; set; } = true;

    public DateTime? LastLoginAt { get; set; }

    [MaxLength(50)]
    public string? LastLoginIP { get; set; }

    public int FailedLoginCount { get; set; } = 0;

    // ── IAuditableEntity ─────────────────────────────────────────────────

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation Properties ─────────────────────────────────────────────

    [ForeignKey(nameof(ReferredByUserId))]
    public virtual ApplicationUser? ReferredBy { get; set; }

    [ForeignKey(nameof(KYCVerifiedByUserId))]
    public virtual ApplicationUser? KYCVerifiedBy { get; set; }

    public virtual ICollection<GoldLoan> GoldLoans { get; set; } = new List<GoldLoan>();
    public virtual ICollection<SalesOrder> SalesOrders { get; set; } = new List<SalesOrder>();
    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();
    public virtual ICollection<LoanRepayment> LoanRepayments { get; set; } = new List<LoanRepayment>();
}
