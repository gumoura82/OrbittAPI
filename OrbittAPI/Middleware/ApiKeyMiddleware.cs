using OrbittAPI.Domain.Interfaces;

namespace OrbittAPI.Middleware;

/// <summary>
/// Middleware de autenticação. Aceita duas formas:
///   1. Header X-Api-Key: {chave}            — uso programático (integrações)
///   2. Header Authorization: Bearer {jwt}   — uso interativo (após login)
/// Rotas públicas são liberadas sem credencial.
/// </summary>
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    /// Rotas públicas. Comparação é por igualdade ou por prefixo apenas para os marcados com 'true'.
    /// Evita o bug clássico de StartsWith liberar rotas filhas indevidamente
    /// (ex.: /api/plans/upgrade caindo no prefixo /api/plans).
    /// </summary>
    private static readonly (string Pattern, bool IsPrefix)[] _publicRoutes =
    {
        ("/swagger",            true),   // Swagger UI e assets
        ("/health",             true),   // Health check
        ("/api/auth/register",  false),  // POST exato
        ("/api/auth/login",     false),  // POST exato
        ("/api/plans",          false),  // GET exato — /api/plans/upgrade NÃO entra aqui
    };

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context,
        IApiKeyRepository apiKeyRepo, IAuthService auth)
    {
        var path = context.Request.Path.Value ?? "";

        if (IsPublicRoute(path))
        {
            await _next(context);
            return;
        }

        // 1) Tenta autenticar por JWT (Authorization: Bearer ...)
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(authHeader) &&
            authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = authHeader["Bearer ".Length..].Trim();
            var userId = auth.ValidateToken(token);
            if (userId.HasValue)
            {
                context.Items["UserId"] = userId.Value;
                context.Items["AuthMethod"] = "JWT";
                await _next(context);
                return;
            }
        }

        // 2) Tenta autenticar por API Key (X-Api-Key)
        if (context.Request.Headers.TryGetValue("X-Api-Key", out var keyValue) &&
            !string.IsNullOrWhiteSpace(keyValue))
        {
            var apiKey = await apiKeyRepo.GetByValueAsync(keyValue!);
            if (apiKey != null && apiKey.IsActive())
            {
                context.Items["UserId"] = apiKey.UserId;
                context.Items["AuthMethod"] = "ApiKey";
                await _next(context);
                return;
            }
        }

        await WriteUnauthorizedAsync(context, path);
    }

    private static bool IsPublicRoute(string path)
    {
        foreach (var (pattern, isPrefix) in _publicRoutes)
        {
            if (isPrefix && path.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                return true;
            if (!isPrefix && string.Equals(path, pattern, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    private static async Task WriteUnauthorizedAsync(HttpContext context, string path)
    {
        context.Response.StatusCode = 401;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsJsonAsync(new
        {
            type = "https://httpstatuses.io/401",
            title = "Unauthorized",
            status = 401,
            detail = "Autenticação ausente ou inválida. " +
                     "Envie X-Api-Key: {sua_chave} ou Authorization: Bearer {jwt}.",
            instance = path,
            timestamp = DateTime.UtcNow.ToString("O")
        });
    }
}
