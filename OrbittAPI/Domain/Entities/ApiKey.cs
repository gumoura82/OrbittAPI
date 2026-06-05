using OrbittAPI.Domain.Enums;

namespace OrbittAPI.Domain.Entities;

/// <summary>
/// Representa uma chave de API associada a um usuário.
/// </summary>
public class ApiKey : BaseEntity
{
    public string KeyValue { get; private set; } = null!;
    public ApiKeyStatus Status { get; private set; }
    public DateTime? RevokedAt { get; private set; }
    public string? RevokedReason { get; private set; }
    public Guid UserId { get; private set; }
    public virtual User User { get; private set; } = null!;

    private ApiKey() { }

    public ApiKey(Guid userId)
    {
        UserId = userId;
        KeyValue = GenerateKey();
        Status = ApiKeyStatus.Active;
    }

    public bool IsActive() => Status == ApiKeyStatus.Active;

    public void Revoke(string reason)
    {
        if (Status == ApiKeyStatus.Revoked)
            throw new InvalidOperationException("Esta API Key já foi revogada.");

        Status = ApiKeyStatus.Revoked;
        RevokedAt = DateTime.UtcNow;
        RevokedReason = reason;
        MarkAsUpdated();
    }

    private static string GenerateKey()
    {
        var prefix = "orbitt";
        var token = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .ToLower();
        return $"{prefix}_{token}_{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}";
    }
}
