using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Admin-configurable interest rate settings.
/// One record is always active (EffectiveTo = null).
/// New settings do NOT retroactively change existing loans — each loan
/// stores a snapshot FK to the InterestSetting at loan creation time.
/// </summary>
public class InterestSetting : BaseEntity, IAuditableEntity
{
    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Rate Configuration ────────────────────────────────────────────────

    /// <summary>E.g., 2.00 = 2% per calculation period.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal InterestRatePercent { get; set; }

    public InterestCalculationType CalculationType { get; set; } = InterestCalculationType.Monthly;

    /// <summary>If true, unpaid interest is added to principal each period.</summary>
    public bool CompoundingEnabled { get; set; } = false;

    /// <summary>Extra interest rate applied once a loan passes MaturityDate.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal PenaltyRatePercent { get; set; } = 1.00m;

    // ── Loan Defaults ─────────────────────────────────────────────────────

    /// <summary>Default loan tenure in months (admin can override per loan).</summary>
    public int DefaultTenureMonths { get; set; } = 12;

    /// <summary>Maximum loan value as a percentage of gold market value.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal LoanToValuePercent { get; set; } = 75.00m;

    // ── Effectivity ───────────────────────────────────────────────────────

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>Null = currently active setting.</summary>
    public DateOnly? EffectiveTo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    // ── IAuditableEntity ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
    public virtual ICollection<GoldLoan> GoldLoans { get; set; } = new List<GoldLoan>();
}
