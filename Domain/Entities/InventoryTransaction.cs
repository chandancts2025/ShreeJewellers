using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Immutable ledger of every stock movement.
/// Never deleted — only marked with a reversal entry if correction needed.
/// </summary>
public class InventoryTransaction : BaseEntity
{
    // ── FKs ───────────────────────────────────────────────────────────────
    public int ProductId { get; set; }

    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Transaction Details ───────────────────────────────────────────────

    public TransactionType TransactionType { get; set; }

    public int Quantity { get; set; }   // +ve = stock in, -ve = stock out

    [Column(TypeName = "decimal(10,3)")]
    public decimal WeightGrams { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal RatePerGram { get; set; }

    [Column(TypeName = "decimal(14,2)")]
    public decimal TotalValue { get; set; }  // Computed: Weight × Rate

    /// <summary>Links back to SalesOrder.OrderNumber, GoldLoan.LoanNumber, etc.</summary>
    [MaxLength(50)]
    public string? ReferenceNo { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual Product Product { get; set; } = null!;
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
}
