using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Core business entity for the gold pledge loan module.
/// Customer deposits physical gold items as collateral and receives cash.
/// Interest accrues per the active InterestSetting at time of loan creation.
/// </summary>
public class GoldLoan : BaseEntity, IAuditableEntity
{
    // ── FKs ───────────────────────────────────────────────────────────────

    [Required]
    [MaxLength(450)]
    public string CustomerUserId { get; set; } = string.Empty;

    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    /// <summary>
    /// Snapshot of the interest setting active when this loan was created.
    /// Stored separately so future admin rate changes do not retroactively alter existing loans.
    /// </summary>
    public int InterestSettingId { get; set; }

    // ── Loan Identity ─────────────────────────────────────────────────────

    /// <summary>Auto-generated: GL-2024-0001</summary>
    [Required]
    [MaxLength(30)]
    public string LoanNumber { get; set; } = string.Empty;

    // ── Dates ─────────────────────────────────────────────────────────────

    public DateOnly LoanDate { get; set; }
    public DateOnly MaturityDate { get; set; }
    public DateOnly? ClosedDate { get; set; }

    // ── Gold Collateral Details ───────────────────────────────────────────

    [Column(TypeName = "decimal(10,3)")]
    public decimal GoldDepositedWeightGrams { get; set; }

    [Required]
    [MaxLength(20)]
    public string GoldPurity { get; set; } = string.Empty;  // 22K / 24K etc.

    /// <summary>Market value of deposited gold on the day the loan was issued.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal GoldCurrentValueAtDeposit { get; set; }

    // ── Loan Financials ───────────────────────────────────────────────────

    /// <summary>Cash disbursed to the customer.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal PrincipalAmount { get; set; }

    /// <summary>E.g., 75 means the loan is 75% of gold's market value.</summary>
    [Column(TypeName = "decimal(5,2)")]
    public decimal LoanToValuePercent { get; set; }

    /// <summary>Running total of all repayments recorded against this loan.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalRepaid { get; set; } = 0;

    // ── Status ────────────────────────────────────────────────────────────

    public LoanStatus LoanStatus { get; set; } = LoanStatus.Active;

    public int ExtensionCount { get; set; } = 0;

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── IAuditableEntity ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser Customer { get; set; } = null!;
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
    public virtual InterestSetting InterestSetting { get; set; } = null!;
    public virtual ICollection<GoldLoanItem> GoldItems { get; set; } = new List<GoldLoanItem>();
    public virtual ICollection<LoanRepayment> Repayments { get; set; } = new List<LoanRepayment>();
}

/// <summary>
/// Individual physical gold item deposited as collateral for a GoldLoan.
/// Each piece (ring, bangle, chain, coin, etc.) tracked separately with photo evidence.
/// </summary>
public class GoldLoanItem : BaseEntity
{
    // ── FK ────────────────────────────────────────────────────────────────
    public int GoldLoanId { get; set; }

    // ── Item Details ──────────────────────────────────────────────────────

    [Required]
    [MaxLength(500)]
    public string ItemDescription { get; set; } = string.Empty;  // e.g., "Gold ring, floral design"

    [Column(TypeName = "decimal(10,3)")]
    public decimal WeightGrams { get; set; }

    [Required]
    [MaxLength(20)]
    public string Purity { get; set; } = string.Empty;

    [Column(TypeName = "decimal(14,2)")]
    public decimal EstimatedValue { get; set; }

    [MaxLength(500)]
    public string? ImageUrl { get; set; }  // Photo of the pledged item

    [MaxLength(200)]
    public string? HallmarkNumber { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual GoldLoan GoldLoan { get; set; } = null!;
}
