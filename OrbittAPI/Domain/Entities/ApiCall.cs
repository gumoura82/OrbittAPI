namespace OrbittAPI.Domain.Entities;

/// <summary>
/// Registra cada chamada feita à API. Usado para billing e dashboard.
/// </summary>
public class ApiCall : BaseEntity
{
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = null!;
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int HttpStatusCode { get; private set; }
    public long ResponseTimeMs { get; private set; }
    public DateTime CalledAt { get; private set; }

    public virtual User User { get; private set; } = null!;

    private ApiCall() { }

    public ApiCall(Guid userId, string endpoint, double lat, double lng, int statusCode, long responseTimeMs)
    {
        UserId = userId;
        Endpoint = endpoint;
        Latitude = lat;
        Longitude = lng;
        HttpStatusCode = statusCode;
        ResponseTimeMs = responseTimeMs;
        CalledAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Retorna o timestamp formatado no fuso horário de Brasília (UTC-3).
    /// </summary>
    public string GetBrasiliaTimestamp()
    {
        var brasiliaZone = TimeZoneInfo.FindSystemTimeZoneById("America/Sao_Paulo");
        var local = TimeZoneInfo.ConvertTimeFromUtc(CalledAt, brasiliaZone);
        return local.ToString("dd/MM/yyyy HH:mm:ss 'BRT'");
    }
}
