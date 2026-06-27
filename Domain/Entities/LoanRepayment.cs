using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Records each cash payment received against a GoldLoan.
/// Payments are allocated interest-first, then to principal (standard practice).
/// This entity is immutable after creation — errors require a reversal entry.
/// </summary>
public class LoanRepayment : BaseEntity
{
    // ── FKs ───────────────────────────────────────────────────────────────
    public int GoldLoanId { get; set; }

    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Repayment Details ─────────────────────────────────────────────────

    public DateOnly RepaymentDate { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal AmountPaid { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PrincipalComponent { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal InterestComponent { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal PenaltyAmount { get; set; } = 0;

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;

    /// <summary>Auto-generated receipt: RCP-2024-0001</summary>
    [Required]
    [MaxLength(30)]
    public string ReceiptNumber { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Notes { get; set; }

    /// <summary>
    /// Running principal balance AFTER this repayment.
    /// Stored for fast outstanding calculation without replaying all repayments.
    /// </summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal PrincipalBalanceAfter { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual GoldLoan GoldLoan { get; set; } = null!;
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
}
