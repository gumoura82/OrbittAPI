using OrbittAPI.Domain.Enums;

namespace OrbittAPI.Domain.Entities;

/// <summary>
/// Representa um usuário/desenvolvedor da plataforma OrbittAPI.
/// Herda de BaseEntity (herança com sentido conceitual).
/// </summary>
public class User : BaseEntity
{
    public string Name { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public SubscriptionPlan Plan { get; private set; }
    public bool MfaEnabled { get; private set; }
    public string? MfaSecret { get; private set; }
    public bool IsActive { get; private set; }

    // Relacionamentos
    public virtual ICollection<ApiKey> ApiKeys { get; private set; } = new List<ApiKey>();
    public virtual ICollection<ApiCall> ApiCalls { get; private set; } = new List<ApiCall>();

    // Limites por plano (encapsulamento)
    private static readonly Dictionary<SubscriptionPlan, int> _planLimits = new()
    {
        { SubscriptionPlan.Free,       500   },
        { SubscriptionPlan.Startup,    10_000 },
        { SubscriptionPlan.Business,   100_000 },
        { SubscriptionPlan.Enterprise, int.MaxValue }
    };

    // Construtor privado para EF Core
    private User() { }

    public User(string name, string email, string passwordHash)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        Email = email?.ToLowerInvariant() ?? throw new ArgumentNullException(nameof(email));
        PasswordHash = passwordHash ?? throw new ArgumentNullException(nameof(passwordHash));
        Plan = SubscriptionPlan.Free;
        IsActive = true;
    }

    public int GetMonthlyCallLimit() => _planLimits[Plan];

    public void Upgrade(SubscriptionPlan newPlan)
    {
        if (newPlan <= Plan)
            throw new InvalidOperationException($"Não é possível fazer downgrade de {Plan} para {newPlan}.");
        Plan = newPlan;
        MarkAsUpdated();
    }

    public void EnableMfa(string secret)
    {
        MfaSecret = secret ?? throw new ArgumentNullException(nameof(secret));
        MfaEnabled = true;
        MarkAsUpdated();
    }

    public void Deactivate()
    {
        IsActive = false;
        MarkAsUpdated();
    }
}
