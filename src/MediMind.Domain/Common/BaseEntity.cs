namespace MediMind.Domain.Common;

/// <summary>
/// Base entity for all domain entities.
/// Provides audit trail timestamps matching database schema (created_at, updated_at).
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; protected set; } = DateTime.UtcNow;

    private readonly List<IDomainEvent> _domainEvents = [];
    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents.AsReadOnly();

    protected void AddDomainEvent(IDomainEvent domainEvent) =>
        _domainEvents.Add(domainEvent);

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void UpdateTimestamp() => UpdatedAt = DateTime.UtcNow;
}

/// <summary>
/// Marker interface for domain events.
/// All domain events are dispatched after the transaction commits.
/// </summary>
public interface IDomainEvent;
