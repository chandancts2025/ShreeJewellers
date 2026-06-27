namespace ShreeJewellers.Domain.Common;

/// <summary>
/// Marks an entity as auditable — EF Core SaveChanges interceptor
/// will auto-populate CreatedAt / UpdatedAt.
/// </summary>
public interface IAuditableEntity
{
    DateTime CreatedAt { get; set; }
    DateTime? UpdatedAt { get; set; }
}
