namespace FarmPlatform.Domain.Common;

/// <summary>
/// Base class for aggregate entities. Ids are UUIDv7: globally unique, safe to expose in URLs,
/// and time-ordered so they index well in PostgreSQL (unlike random UUIDv4).
/// </summary>
public abstract class Entity : IAuditable
{
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    /// <summary>Set by the persistence layer when the row is first inserted.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Set by the persistence layer on every insert/update.</summary>
    public DateTimeOffset UpdatedAt { get; set; }
}
