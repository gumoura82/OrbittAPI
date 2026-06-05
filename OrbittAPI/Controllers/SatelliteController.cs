using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using OrbittAPI.Application.DTOs;
using OrbittAPI.Domain.Entities;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Controllers;

/// <summary>
/// Controller principal de dados satelitais.
/// Cobre US-05 (/landuse), US-06 (/vegetation), US-07 (/flood-risk),
/// US-08 (/deforestation), US-09 (/urban-growth), US-10 (erros padronizados).
/// </summary>
[Route("api/satellite")]
public class SatelliteController : OrbittBaseController
{
    private readonly ISatelliteDataService _satellite;
    private readonly IUserRepository _users;
    private readonly IApiCallRepository _calls;
    private readonly ILogger<SatelliteController> _logger;

    public SatelliteController(ISatelliteDataService satellite, IUserRepository users,
        IApiCallRepository calls, ILogger<SatelliteController> logger)
    {
        _satellite = satellite;
        _users = users;
        _calls = calls;
        _logger = logger;
    }

    /// <summary>
    /// US-05 — Retorna tipo de uso do solo para as coordenadas informadas.
    /// </summary>
    [HttpGet("landuse")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(429)]
    public async Task<IActionResult> GetLandUse([FromQuery] double lat, [FromQuery] double lng)
    {
        var userId = GetCurrentUserId();
        await CheckQuotaAsync(userId);

        var sw = Stopwatch.StartNew();
        var data = await _satellite.GetLandUseAsync(lat, lng);
        sw.Stop();

        await RecordCallAsync(userId, "/landuse", lat, lng, 200, sw.ElapsedMilliseconds);

        return Ok(new
        {
            endpoint = "/landuse",
            data.Latitude,
            data.Longitude,
            lastImageDate = data.FormattedCaptureDate(),
            data.DataSource,
            landUse = new
            {
                dominantType = data.DominantType.ToString(),
                vegetationPct = data.VegetationPct,
                urbanPct = data.UrbanPct,
                waterPct = data.WaterPct,
                exposedSoilPct = data.ExposedSoilPct
            },
            alert = data.GetAlertDescription(),
            responseTimeMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// US-06 — Retorna índice NDVI (vegetação) para as coordenadas.
    /// </summary>
    [HttpGet("vegetation")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetVegetation([FromQuery] double lat, [FromQuery] double lng)
    {
        var userId = GetCurrentUserId();
        await CheckQuotaAsync(userId);

        var sw = Stopwatch.StartNew();
        var data = await _satellite.GetVegetationAsync(lat, lng);
        sw.Stop();

        await RecordCallAsync(userId, "/vegetation", lat, lng, 200, sw.ElapsedMilliseconds);

        return Ok(new
        {
            endpoint = "/vegetation",
            data.Latitude,
            data.Longitude,
            lastImageDate = data.FormattedCaptureDate(),
            data.DataSource,
            ndvi = new
            {
                value = data.NdviIndex,
                healthStatus = data.HealthStatus,
                scale = "NDVI varia de -1 (sem vegetação) a +1 (vegetação densa)"
            },
            alert = data.GetAlertDescription(),
            alertLevel = data.AlertLevel?.ToString(),
            responseTimeMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// US-07 — Retorna histórico de risco de alagamento para a região.
    /// </summary>
    [HttpGet("flood-risk")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetFloodRisk([FromQuery] double lat, [FromQuery] double lng)
    {
        var userId = GetCurrentUserId();
        await CheckQuotaAsync(userId);

        var sw = Stopwatch.StartNew();
        var data = await _satellite.GetFloodRiskAsync(lat, lng);
        sw.Stop();

        await RecordCallAsync(userId, "/flood-risk", lat, lng, 200, sw.ElapsedMilliseconds);

        return Ok(new
        {
            endpoint = "/flood-risk",
            data.Latitude,
            data.Longitude,
            data.DataSource,
            riskScore = data.RiskScore,
            alertLevel = data.AlertLevel?.ToString(),
            alert = data.GetAlertDescription(),
            history = data.History.Select(h => new
            {
                h.Year,
                h.FloodOccurred,
                h.MaxRiskScore,
                floodDate = h.FloodDate?.ToString("yyyy-MM-dd")
            }),
            responseTimeMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// US-08 — Detecta desmatamento recente em uma área no período especificado.
    /// </summary>
    [HttpGet("deforestation")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetDeforestation(
        [FromQuery] double lat, [FromQuery] double lng,
        [FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var userId = GetCurrentUserId();
        await CheckQuotaAsync(userId);

        var fromDate = from ?? DateTime.UtcNow.AddYears(-1);
        var toDate = to ?? DateTime.UtcNow;

        var sw = Stopwatch.StartNew();
        var data = await _satellite.GetDeforestationAsync(lat, lng, fromDate, toDate);
        sw.Stop();

        await RecordCallAsync(userId, "/deforestation", lat, lng, 200, sw.ElapsedMilliseconds);

        return Ok(new
        {
            endpoint = "/deforestation",
            data.Latitude,
            data.Longitude,
            data.DataSource,
            period = new { from = data.PeriodStart.ToString("yyyy-MM-dd"), to = data.PeriodEnd.ToString("yyyy-MM-dd") },
            deforestedAreaKm2 = data.DeforestedAreaKm2,
            alertLevel = data.AlertLevel?.ToString(),
            alert = data.GetAlertDescription(),
            alerts = data.Alerts.Select(a => new
            {
                detectedAt = a.DetectedAt.ToString("yyyy-MM-dd"),
                areaKm2 = a.AreaKm2,
                satelliteImageRef = a.SatelliteImageRef
            }),
            responseTimeMs = sw.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// US-09 — Retorna dados anuais de expansão urbana nos últimos 10 anos.
    /// </summary>
    [HttpGet("urban-growth")]
    [ProducesResponseType(200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> GetUrbanGrowth([FromQuery] double lat, [FromQuery] double lng)
    {
        var userId = GetCurrentUserId();
        await CheckQuotaAsync(userId);

        var sw = Stopwatch.StartNew();
        var data = await _satellite.GetUrbanGrowthAsync(lat, lng);
        sw.Stop();

        await RecordCallAsync(userId, "/urban-growth", lat, lng, 200, sw.ElapsedMilliseconds);

        return Ok(new
        {
            endpoint = "/urban-growth",
            data.Latitude,
            data.Longitude,
            data.DataSource,
            totalGrowthPct = data.TotalGrowthPct,
            alert = data.GetAlertDescription(),
            yearlyData = data.YearlyData.Select(y => new
            {
                y.Year,
                urbanAreaKm2 = y.UrbanAreaKm2,
                growthPct = y.GrowthPctVsPreviousYear
            }),
            responseTimeMs = sw.ElapsedMilliseconds
        });
    }

    // --- Helpers privados ---

    private async Task CheckQuotaAsync(Guid userId)
    {
        var user = await _users.GetByIdAsync(userId)
            ?? throw new NotFoundException("Usuário", userId);

        var now = DateTime.UtcNow;
        var count = await _calls.GetMonthlyCountAsync(userId, now.Year, now.Month);
        var limit = user.GetMonthlyCallLimit();

        if (count >= limit)
            throw new QuotaExceededException(limit, count);

        // Alerta ao atingir 80% (log — em produção enviaria e-mail/dashboard)
        var pct = (double)count / limit * 100;
        if (pct >= 80 && pct < 100)
            _logger.LogWarning("[Quota] Usuário {UserId} atingiu {Pct:0.0}% da quota mensal ({Count}/{Limit})",
                userId, pct, count, limit);
    }

    private async Task RecordCallAsync(Guid userId, string endpoint, double lat, double lng,
        int status, long ms)
    {
        // Retry com backoff exponencial para falhas transitórias do banco.
        // Demonstra uso de while clássico com contador e condição de saída.
        const int maxAttempts = 3;
        int attempt = 0;
        var call = new ApiCall(userId, endpoint, lat, lng, status, ms);

        while (attempt < maxAttempts)
        {
            attempt++;
            try
            {
                await _calls.RecordAsync(call);
                return; // sucesso, sai do loop
            }
            catch (Exception ex) when (attempt < maxAttempts)
            {
                // Backoff: 100ms, 200ms, 400ms...
                var delayMs = 100 * (int)Math.Pow(2, attempt - 1);
                _logger.LogWarning(ex,
                    "[ApiCall] Tentativa {Attempt}/{Max} falhou. Retentando em {Delay}ms...",
                    attempt, maxAttempts, delayMs);
                await Task.Delay(delayMs);
            }
            catch (Exception ex)
            {
                // Última tentativa falhou — registra como erro mas não bloqueia a resposta
                _logger.LogError(ex,
                    "[ApiCall] Falha definitiva ao registrar chamada do usuário {UserId} após {Attempts} tentativas",
                    userId, attempt);
                return;
            }
        }
    }
}
