using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Jewellery product with full hallmark, weight, and pricing details.
/// Supports gold, silver, diamond and other metal types.
/// </summary>
public class Product : BaseEntity, IAuditableEntity
{
    // ── FK ───────────────────────────────────────────────────────────────
    public int CategoryId { get; set; }

    // ── Identification ───────────────────────────────────────────────────

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    /// <summary>Unique Stock Keeping Unit code (e.g., GLD-22K-RING-001)</summary>
    [Required]
    [MaxLength(50)]
    public string SKUCode { get; set; } = string.Empty;

    /// <summary>GST HSN code (e.g., 7113 for gold jewellery)</summary>
    [Required]
    [MaxLength(20)]
    public string HSNCode { get; set; } = string.Empty;

    // ── Metal & Quality ───────────────────────────────────────────────────

    /// <summary>E.g., 22K, 24K, 18K, 925 (sterling silver)</summary>
    [Required]
    [MaxLength(20)]
    public string Purity { get; set; } = string.Empty;

    [Column(TypeName = "decimal(10,3)")]
    public decimal NetWeightGrams { get; set; }      // Metal weight only

    [Column(TypeName = "decimal(10,3)")]
    public decimal GrossWeightGrams { get; set; }    // Including stones

    [Column(TypeName = "decimal(10,3)")]
    public decimal StoneWeightGrams { get; set; } = 0;

    [MaxLength(50)]
    public string? HallmarkNumber { get; set; }      // BIS hallmark number

    // ── Pricing ───────────────────────────────────────────────────────────

    public MakingChargesType MakingChargesType { get; set; } = MakingChargesType.PerGram;

    [Column(TypeName = "decimal(10,2)")]
    public decimal MakingChargesValue { get; set; }  // Per gram OR percentage value

    [Column(TypeName = "decimal(5,2)")]
    public decimal WastagePercent { get; set; } = 0;

    [Column(TypeName = "decimal(10,2)")]
    public decimal HallmarkCharges { get; set; } = 0;

    [Column(TypeName = "decimal(5,2)")]
    public decimal GSTRatePercent { get; set; } = 3; // 3% for gold/silver

    [Column(TypeName = "decimal(10,2)")]
    public decimal StoneValue { get; set; } = 0;

    // ── Stock ─────────────────────────────────────────────────────────────

    public int StockQuantity { get; set; } = 0;
    public int ReorderLevel { get; set; } = 2;

    // ── Media ─────────────────────────────────────────────────────────────

    [MaxLength(500)]
    public string? ImageUrl { get; set; }

    [MaxLength(200)]
    public string? BarcodeData { get; set; }  // QR code / barcode string

    // ── Status ────────────────────────────────────────────────────────────

    public bool IsActive { get; set; } = true;

    // ── IAuditableEntity ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual Category Category { get; set; } = null!;
    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();
    public virtual ICollection<SalesOrderItem> SalesOrderItems { get; set; } = new List<SalesOrderItem>();
}
