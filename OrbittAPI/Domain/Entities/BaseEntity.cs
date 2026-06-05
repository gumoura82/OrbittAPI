namespace OrbittAPI.Domain.Entities;

/// <summary>
/// Classe base abstrata para todas as entidades do domínio.
/// Aplica o princípio de abstração e herança da POO.
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }

    public void MarkAsUpdated() => UpdatedAt = DateTime.UtcNow;
}
