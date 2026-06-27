using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

// ─────────────────────────────────────────────────────────────────────────────
// GoldPriceHistory
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Timestamped record of gold and silver market prices.
/// Refreshed every 15 minutes by PriceRefreshService via GoldAPI.io.
/// Manually overridable by Admin for the trading day.
/// </summary>
public class GoldPriceHistory : BaseEntity
{
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;

    /// <summary>22K gold per 10 grams in INR.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal GoldRatePer10Gram_22K { get; set; }

    /// <summary>24K (pure) gold per 10 grams in INR.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal GoldRatePer10Gram_24K { get; set; }

    /// <summary>Silver per kilogram in INR.</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal SilverRatePerKg { get; set; }

    public PriceSource Source { get; set; } = PriceSource.API;

    /// <summary>Null when auto-fetched by background job.</summary>
    [MaxLength(450)]
    public string? CreatedByUserId { get; set; }

    public bool IsManualOverride { get; set; } = false;

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser? CreatedBy { get; set; }
}


// ─────────────────────────────────────────────────────────────────────────────
// Notification
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// All outbound notifications sent to users (SMS, Email, InApp).
/// Used for loan due date reminders, repayment confirmations, KYC updates.
/// </summary>
public class Notification : BaseEntity
{
    [Required]
    [MaxLength(450)]
    public string UserId { get; set; } = string.Empty;

    public NotificationType Type { get; set; }

    [Required]
    [MaxLength(300)]
    public string Subject { get; set; } = string.Empty;

    [Required]
    public string Body { get; set; } = string.Empty;

    public DateTime? SentAt { get; set; }

    public bool IsRead { get; set; } = false;
    public DateTime? ReadAt { get; set; }

    /// <summary>E.g., "GoldLoan", "SalesOrder" — for deep-linking.</summary>
    [MaxLength(100)]
    public string? RelatedEntityType { get; set; }

    public int? RelatedEntityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser User { get; set; } = null!;
}


// ─────────────────────────────────────────────────────────────────────────────
// AuditLog
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Immutable financial audit trail.
/// Written by AuditLoggingMiddleware and directly in service layer for
/// sensitive operations (loan creation, repayment recording, KYC changes).
/// Never deleted or updated.
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>Null for system/background job actions.</summary>
    [MaxLength(450)]
    public string? UserId { get; set; }

    [Required]
    [MaxLength(100)]
    public string Action { get; set; } = string.Empty;  // e.g., "CREATE", "UPDATE", "KYC_VERIFY"

    [Required]
    [MaxLength(100)]
    public string EntityName { get; set; } = string.Empty;  // e.g., "GoldLoan"

    [MaxLength(100)]
    public string? EntityId { get; set; }

    /// <summary>JSON snapshot of entity state before change. Null for CREATE.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? OldValues { get; set; }

    /// <summary>JSON snapshot of entity state after change. Null for DELETE.</summary>
    [Column(TypeName = "nvarchar(max)")]
    public string? NewValues { get; set; }

    [MaxLength(50)]
    public string? IPAddress { get; set; }

    [MaxLength(500)]
    public string? UserAgent { get; set; }

    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser? User { get; set; }
}


// ─────────────────────────────────────────────────────────────────────────────
// AppSetting
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Key-value store for runtime-configurable shop settings.
/// SuperAdmin can update these via the Settings page without redeployment.
/// Examples: ShopName, GSTNumber, LogoUrl, DefaultCurrency, SMSEnabled.
/// </summary>
public class AppSetting : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string SettingKey { get; set; } = string.Empty;  // Unique key

    [Required]
    [MaxLength(2000)]
    public string SettingValue { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public bool IsEncrypted { get; set; } = false;  // For sensitive config values

    [MaxLength(450)]
    public string? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser? UpdatedBy { get; set; }
}
