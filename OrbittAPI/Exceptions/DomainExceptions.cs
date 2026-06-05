namespace OrbittAPI.Exceptions;

/// <summary>
/// Exceção lançada quando um recurso não é encontrado (HTTP 404).
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string resource, object id)
        : base($"{resource} com identificador '{id}' não foi encontrado.") { }
}

/// <summary>
/// Exceção lançada para violações de negócio (HTTP 400).
/// </summary>
public class BusinessException : Exception
{
    public BusinessException(string message) : base(message) { }
}

/// <summary>
/// Exceção lançada quando acesso não autorizado (HTTP 401).
/// </summary>
public class UnauthorizedException : Exception
{
    public UnauthorizedException(string message = "Acesso não autorizado.") : base(message) { }
}

/// <summary>
/// Exceção lançada quando o usuário está autenticado mas não tem permissão para o recurso (HTTP 403).
/// Diferente de UnauthorizedException (401): aqui o usuário é conhecido, mas não tem direito ao recurso.
/// </summary>
public class ForbiddenException : Exception
{
    public ForbiddenException(string message = "Você não tem permissão para acessar este recurso.") : base(message) { }
}

/// <summary>
/// Exceção lançada quando o limite de chamadas da API é atingido (HTTP 429).
/// </summary>
public class QuotaExceededException : Exception
{
    public int Limit { get; }
    public int CurrentUsage { get; }

    public QuotaExceededException(int limit, int current)
        : base($"Limite mensal de {limit} chamadas atingido. Uso atual: {current}. Faça upgrade do plano.")
    {
        Limit = limit;
        CurrentUsage = current;
    }
}

/// <summary>
/// Exceção para coordenadas geográficas inválidas.
/// </summary>
public class InvalidCoordinatesException : Exception
{
    public InvalidCoordinatesException(double lat, double lng)
        : base($"Coordenadas inválidas: latitude={lat}, longitude={lng}. " +
               "Latitude deve estar entre -90 e 90, longitude entre -180 e 180.") { }
}
