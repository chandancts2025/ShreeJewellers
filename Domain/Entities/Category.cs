using ShreeJewellers.Domain.Common;
using ShreeJewellers.Domain.Enums;
using System.ComponentModel.DataAnnotations;

namespace ShreeJewellers.Domain.Entities;

/// <summary>
/// Product category (Gold Jewellery, Silver Items, Diamond, Platinum, Coins/Bars).
/// </summary>
public class Category : BaseEntity, IAuditableEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public MetalType MetalType { get; set; } = MetalType.Gold;

    public bool IsActive { get; set; } = true;

    // ── IAuditableEntity ─────────────────────────────────────────────────
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // ── Navigation ───────────────────────────────────────────────────────
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
}
