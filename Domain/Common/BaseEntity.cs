namespace ShreeJewellers.Domain.Common;

/// <summary>
/// Base class for all domain entities with a typed primary key.
/// </summary>
public abstract class BaseEntity<TKey>
{
    public TKey Id { get; set; } = default!;
}

/// <summary>
/// Convenience base for int-keyed entities (most tables).
/// </summary>
public abstract class BaseEntity : BaseEntity<int> { }
