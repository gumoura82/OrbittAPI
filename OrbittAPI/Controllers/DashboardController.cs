using Microsoft.AspNetCore.Mvc;
using OrbittAPI.Application.DTOs;
using OrbittAPI.Domain.Enums;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Controllers;

/// <summary>
/// Controller de dashboard e monitoramento do cliente.
/// Cobre US-11 (consumo), US-12 (mapa), US-13 (exportação), US-14 (alertas de quota).
/// </summary>
[Route("api/dashboard")]
public class DashboardController : OrbittBaseController
{
    private readonly IUserRepository _users;
    private readonly IApiCallRepository _calls;
    private readonly ILogger<DashboardController> _logger;

    public DashboardController(IUserRepository users, IApiCallRepository calls,
        ILogger<DashboardController> logger)
    {
        _users = users;
        _calls = calls;
        _logger = logger;
    }

    /// <summary>
    /// US-11 — Estatísticas de uso do mês atual.
    /// </summary>
    [HttpGet("usage")]
    [ProducesResponseType(typeof(UsageStats), 200)]
    public async Task<IActionResult> GetUsage()
    {
        var userId = GetCurrentUserId();
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("Usuário", userId);

        var now = DateTime.UtcNow;
        var monthlyCount = await _calls.GetMonthlyCountAsync(userId, now.Year, now.Month);
        var limit = user.GetMonthlyCallLimit();
        var pct = limit == int.MaxValue ? 0 : Math.Round((double)monthlyCount / limit * 100, 1);

        // Agrupa por dia para gráfico
        var recentCalls = await _calls.GetByUserAndPeriodAsync(userId,
            new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc),
            now);

        var dailyBreakdown = recentCalls
            .GroupBy(c => c.CalledAt.Date)
            .Select(g => new { date = g.Key.ToString("yyyy-MM-dd"), calls = g.Count() })
            .OrderBy(x => x.date)
            .ToList();

        _logger.LogInformation("[Dashboard] Consulta de uso: user={UserId} count={Count}/{Limit}", userId, monthlyCount, limit);

        return Ok(new
        {
            plan = user.Plan.ToString(),
            currentMonthCalls = monthlyCount,
            monthlyLimit = limit == int.MaxValue ? "Ilimitado" : limit.ToString(),
            usagePercent = pct,
            isNearLimit = pct >= 80,
            alertMessage = pct >= 95 ? "⚠️ Atenção: você está em 95%+ do seu limite mensal!"
                         : pct >= 80 ? "⚠️ Você atingiu 80% do seu limite mensal."
                         : null,
            dailyBreakdown,
            periodStart = new DateTime(now.Year, now.Month, 1).ToString("yyyy-MM-dd"),
            periodEnd = now.ToString("yyyy-MM-dd")
        });
    }

    /// <summary>
    /// US-12 — Últimas 100 consultas com coordenadas para exibição em mapa.
    /// </summary>
    [HttpGet("map")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetMapData(
        [FromQuery] string? endpoint = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = GetCurrentUserId();
        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;

        var calls = await _calls.GetByUserAndPeriodAsync(userId, fromDate, toDate);

        // Filtra por endpoint se informado
        if (!string.IsNullOrWhiteSpace(endpoint))
            calls = calls.Where(c => c.Endpoint.Contains(endpoint, StringComparison.OrdinalIgnoreCase));

        var pins = calls.Take(100).Select(c => new ApiCallSummary(
            CalledAt: c.CalledAt,
            Endpoint: c.Endpoint,
            Latitude: c.Latitude,
            Longitude: c.Longitude,
            HttpStatusCode: c.HttpStatusCode,
            ResponseTimeMs: c.ResponseTimeMs,
            BrasiliaTimestamp: c.GetBrasiliaTimestamp()
        ));

        return Ok(new { totalPins = pins.Count(), pins });
    }

    /// <summary>
    /// US-13 — Exporta histórico de consultas em CSV.
    /// </summary>
    [HttpGet("export/csv")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> ExportCsv(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var userId = GetCurrentUserId();
        var fromDate = from ?? DateTime.UtcNow.AddMonths(-1);
        var toDate = to ?? DateTime.UtcNow;

        var calls = await _calls.GetByUserAndPeriodAsync(userId, fromDate, toDate);

        var csv = BuildCsv(calls);
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileName = $"orbitt_export_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv";

        return File(bytes, "text/csv", fileName);
    }

    private static string BuildCsv(IEnumerable<Domain.Entities.ApiCall> calls)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("timestamp_utc,endpoint,latitude,longitude,http_status,response_time_ms");
        foreach (var c in calls)
        {
            sb.AppendLine($"{c.CalledAt:O},{c.Endpoint},{c.Latitude},{c.Longitude},{c.HttpStatusCode},{c.ResponseTimeMs}");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Controller de planos e billing.
/// Cobre US-15 (planos), US-16 (upgrade), US-17 (faturas).
/// </summary>
[Route("api/plans")]
public class PlansController : OrbittBaseController
{
    private readonly IUserRepository _users;
    private readonly ILogger<PlansController> _logger;

    public PlansController(IUserRepository users, ILogger<PlansController> logger)
    {
        _users = users;
        _logger = logger;
    }

    /// <summary>
    /// US-15 — Listagem pública de planos disponíveis.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<PlanInfo>), 200)]
    public IActionResult GetPlans()
    {
        var plans = new List<PlanInfo>
        {
            new("Free",       500,     "Grátis",   new() { "500 chamadas/mês", "Endpoints básicos", "Suporte por e-mail" }),
            new("Startup",    10_000,  "R$ 99/mês",  new() { "10.000 chamadas/mês", "Todos os endpoints", "Dashboard analítico", "Suporte prioritário" }),
            new("Business",   100_000, "R$ 499/mês", new() { "100.000 chamadas/mês", "Exportação de relatórios", "SLA 99,5%", "API Key múltiplas" }),
            new("Enterprise", -1,      "Consultar",  new() { "Chamadas ilimitadas", "White-label", "SLA 99,9%", "Domínio personalizado", "Suporte dedicado" })
        };

        return Ok(plans);
    }

    /// <summary>
    /// US-16 — Faz upgrade do plano do usuário autenticado.
    /// </summary>
    [HttpPost("upgrade")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpgradePlan([FromBody] UpgradePlanRequest request)
    {
        var userId = GetCurrentUserId();
        var user = await _users.GetByIdAsync(userId) ?? throw new NotFoundException("Usuário", userId);

        user.Upgrade(request.NewPlan);
        await _users.UpdateAsync(user);

        _logger.LogInformation("[Upgrade] Usuário {UserId} fez upgrade para {Plan}", userId, request.NewPlan);

        return Ok(new
        {
            message = $"Upgrade realizado com sucesso! Novo plano: {request.NewPlan}",
            newPlan = request.NewPlan.ToString(),
            effectiveAt = DateTime.UtcNow.ToString("O")
        });
    }
}

/// <summary>
/// Controller administrativo — revogação de chaves (US-03).
/// </summary>
[Route("api/admin")]
public class AdminController : OrbittBaseController
{
    private readonly IApiKeyRepository _apiKeys;
    private readonly ILogger<AdminController> _logger;

    public AdminController(IApiKeyRepository apiKeys, ILogger<AdminController> logger)
    {
        _apiKeys = apiKeys;
        _logger = logger;
    }

    /// <summary>
    /// US-03 — Revoga uma API Key. Valida que a chave pertence ao usuário autenticado.
    /// Lança HTTP 403 se o usuário tentar revogar a chave de outro usuário.
    /// </summary>
    [HttpPost("revoke-key")]
    [ProducesResponseType(200)]
    [ProducesResponseType(403)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> RevokeKey([FromBody] RevokeKeyRequest request)
    {
        var requestingUserId = GetCurrentUserId();

        var apiKey = await _apiKeys.GetByValueAsync(request.ApiKey)
            ?? throw new NotFoundException("API Key", request.ApiKey);

        // HTTP 403: usuário autenticado mas sem permissão sobre este recurso
        if (apiKey.UserId != requestingUserId)
            throw new ForbiddenException("Você não tem permissão para revogar a API Key de outro usuário.");

        apiKey.Revoke(request.Reason);
        await _apiKeys.UpdateAsync(apiKey);

        _logger.LogWarning("[Admin] API Key revogada: {KeyId} — motivo: {Reason}", apiKey.Id, request.Reason);

        return Ok(new
        {
            message = "API Key revogada com sucesso.",
            revokedAt = apiKey.RevokedAt?.ToString("O"),
            reason = request.Reason
        });
    }
}
