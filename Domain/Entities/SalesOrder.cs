using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// A sales invoice / billing transaction. Supports GST, old-gold exchange,
/// split payments, and full return workflow.
/// </summary>
public class SalesOrder : BaseEntity, IAuditableEntity
{
    // ── FKs ───────────────────────────────────────────────────────────────

    /// <summary>Nullable: walk-in customers without an account are allowed.</summary>
    [MaxLength(450)]
    public string? CustomerUserId { get; set; }

    [Required]
    [MaxLength(450)]
    public string CreatedByUserId { get; set; } = string.Empty;

    // ── Order Identity ────────────────────────────────────────────────────

    /// <summary>Auto-generated: INV-2024-07-0001</summary>
    [Required]
    [MaxLength(30)]
    public string OrderNumber { get; set; } = string.Empty;

    public DateTime OrderDate { get; set; } = DateTime.UtcNow;

    // ── Amounts ───────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(14,2)")]
    public decimal GrossAmount { get; set; }              // Before discount & tax

    [Column(TypeName = "decimal(14,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal TaxAmount { get; set; }                // CGST + SGST total

    /// <summary>Value deducted when customer exchanges old gold against purchase.</summary>
    [Column(TypeName = "decimal(14,2)")]
    public decimal OldGoldExchangeValue { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal NetAmount { get; set; }                // Final payable amount

    // ── Payment ───────────────────────────────────────────────────────────

    public PaymentMode PaymentMode { get; set; } = PaymentMode.Cash;
    public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Pending;

    [Column(TypeName = "decimal(14,2)")]
    public decimal AmountPaid { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal AdvanceAmount { get; set; } = 0;       // For booking orders

    // ── Status & Documents ────────────────────────────────────────────────

    public OrderStatus Status { get; set; } = OrderStatus.Draft;

    [MaxLength(500)]
    public string? InvoiceUrl { get; set; }               // Generated PDF path

    [MaxLength(1000)]
    public string? Notes { get; set; }

    // ── GST Fields ────────────────────────────────────────────────────────

    [Column(TypeName = "decimal(14,2)")]
    public decimal CGSTAmount { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal SGSTAmount { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal IGSTAmount { get; set; } = 0;  // For inter-state sales

    // ── IAuditableEntity ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ApplicationUser? Customer { get; set; }
    public virtual ApplicationUser CreatedBy { get; set; } = null!;
    public virtual ICollection<SalesOrderItem> Items { get; set; } = new List<SalesOrderItem>();
}

/// <summary>
/// Individual line item within a SalesOrder.
/// Stores a snapshot of price at time of sale (immutable after confirmation).
/// </summary>
public class SalesOrderItem : BaseEntity
{
    // ── FKs ───────────────────────────────────────────────────────────────
    public int SalesOrderId { get; set; }
    public int ProductId { get; set; }

    // ── Item Details ──────────────────────────────────────────────────────

    public int Quantity { get; set; } = 1;

    [Column(TypeName = "decimal(10,3)")]
    public decimal WeightGrams { get; set; }

    /// <summary>Gold/silver rate per gram at the time of sale (price snapshot).</summary>
    [Column(TypeName = "decimal(10,2)")]
    public decimal RatePerGram { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal MakingCharges { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal HallmarkCharges { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal StoneValue { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal TaxPercent { get; set; } = 3;

    [Column(TypeName = "decimal(10,2)")]
    public decimal TaxAmount { get; set; }

    [Column(TypeName = "decimal(10,2)")]
    public decimal DiscountAmount { get; set; } = 0;

    [Column(TypeName = "decimal(14,2)")]
    public decimal LineTotal { get; set; }   // Final amount for this line

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual SalesOrder SalesOrder { get; set; } = null!;
    public virtual Product Product { get; set; } = null!;
}
