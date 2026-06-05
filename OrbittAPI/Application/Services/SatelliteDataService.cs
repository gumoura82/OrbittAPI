using OrbittAPI.Domain;
using OrbittAPI.Domain.Entities;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Application.Services;

/// <summary>
/// Serviço de processamento de dados satelitais.
/// Em produção, este serviço se comunicaria com pipelines reais (NASA, ESA, INPE).
/// Aqui simula resultados determinísticos baseados nas coordenadas para demonstração.
/// </summary>
public class SatelliteDataService : ISatelliteDataService
{
    private readonly ILogger<SatelliteDataService> _logger;

    public SatelliteDataService(ILogger<SatelliteDataService> logger) => _logger = logger;

    public Task<LandUseData> GetLandUseAsync(double lat, double lng)
    {
        CoordinateValidator.ThrowIfInvalid(lat, lng);
        _logger.LogInformation("[LandUse] Processando coordenadas lat={Lat} lng={Lng}", lat, lng);

        // Simula variação baseada nas coordenadas (seed determinístico para demo)
        var seed = (int)((Math.Abs(lat) + Math.Abs(lng)) * 100);
        var rng = new Random(seed);

        double veg = Math.Round(rng.NextDouble() * 60 + 10, 1);
        double urban = Math.Round(rng.NextDouble() * 30, 1);
        double water = Math.Round(rng.NextDouble() * 15, 1);
        double soil = Math.Round(Math.Max(0, 100 - veg - urban - water), 1);

        var result = new LandUseData(lat, lng, veg, urban, water, soil);
        return Task.FromResult(result);
    }

    public Task<VegetationData> GetVegetationAsync(double lat, double lng)
    {
        CoordinateValidator.ThrowIfInvalid(lat, lng);
        _logger.LogInformation("[NDVI] Calculando índice de vegetação para lat={Lat} lng={Lng}", lat, lng);

        var seed = (int)((Math.Abs(lat) * Math.Abs(lng)) * 10 + 1);
        var rng = new Random(seed);
        var ndvi = Math.Round(rng.NextDouble() * 1.6 - 0.3, 4); // -0.3 a 1.3, clampado
        ndvi = Math.Clamp(ndvi, -1.0, 1.0);

        var result = new VegetationData(lat, lng, ndvi);
        return Task.FromResult(result);
    }

    public Task<FloodRiskData> GetFloodRiskAsync(double lat, double lng)
    {
        CoordinateValidator.ThrowIfInvalid(lat, lng);
        _logger.LogInformation("[FloodRisk] Analisando risco de alagamento para lat={Lat} lng={Lng}", lat, lng);

        var seed = (int)(Math.Abs(lat + lng) * 50);
        var rng = new Random(seed);
        var riskScore = Math.Round(rng.NextDouble() * 10, 1);

        var history = GenerateFloodHistory(rng);
        var result = new FloodRiskData(lat, lng, riskScore, history);
        return Task.FromResult(result);
    }

    public Task<DeforestationData> GetDeforestationAsync(double lat, double lng, DateTime from, DateTime to)
    {
        CoordinateValidator.ThrowIfInvalid(lat, lng);

        if (from > to)
            throw new BusinessException("A data de início deve ser anterior à data de fim.");
        if ((to - from).TotalDays > 365 * 5)
            throw new BusinessException("O período máximo de consulta é de 5 anos.");

        _logger.LogInformation("[Deforestation] Analisando desmatamento de {From:yyyy-MM-dd} a {To:yyyy-MM-dd}", from, to);

        var seed = (int)(Math.Abs(lat) * 100 + Math.Abs(lng));
        var rng = new Random(seed);
        var areaKm2 = Math.Round(rng.NextDouble() * 25, 2);

        var alerts = GenerateDeforestationAlerts(rng, from, to);
        var result = new DeforestationData(lat, lng, areaKm2, from, to, alerts);
        return Task.FromResult(result);
    }

    public Task<UrbanGrowthData> GetUrbanGrowthAsync(double lat, double lng)
    {
        CoordinateValidator.ThrowIfInvalid(lat, lng);
        _logger.LogInformation("[UrbanGrowth] Analisando crescimento urbano para lat={Lat} lng={Lng}", lat, lng);

        var seed = (int)((Math.Abs(lat) + Math.Abs(lng)) * 30);
        var rng = new Random(seed);
        var yearlyData = GenerateUrbanGrowthHistory(rng);

        var result = new UrbanGrowthData(lat, lng, yearlyData);
        return Task.FromResult(result);
    }

    // --- Métodos privados auxiliares ---

    private static List<FloodHistoryEntry> GenerateFloodHistory(Random rng)
    {
        var history = new List<FloodHistoryEntry>();
        var currentYear = DateTime.UtcNow.Year;

        for (int i = 4; i >= 0; i--)
        {
            var year = currentYear - i;
            var occurred = rng.NextDouble() > 0.6;
            history.Add(new FloodHistoryEntry
            {
                Year = year,
                FloodOccurred = occurred,
                MaxRiskScore = Math.Round(rng.NextDouble() * 10, 1),
                FloodDate = occurred ? new DateTime(year, rng.Next(1, 13), rng.Next(1, 28), 0, 0, 0, DateTimeKind.Utc) : null
            });
        }
        return history;
    }

    private static List<DeforestationAlert> GenerateDeforestationAlerts(Random rng, DateTime from, DateTime to)
    {
        var alerts = new List<DeforestationAlert>();
        var totalDays = (int)(to - from).TotalDays;
        var count = rng.Next(0, 5);

        for (int i = 0; i < count; i++)
        {
            var dayOffset = rng.Next(0, Math.Max(1, totalDays));
            alerts.Add(new DeforestationAlert
            {
                DetectedAt = from.AddDays(dayOffset),
                AreaKm2 = Math.Round(rng.NextDouble() * 5, 2),
                SatelliteImageRef = $"S2A_MSIL2A_{from.AddDays(dayOffset):yyyyMMdd}_T{rng.Next(10, 60):00}UYV"
            });
        }
        return alerts;
    }

    private static List<UrbanGrowthYear> GenerateUrbanGrowthHistory(Random rng)
    {
        var data = new List<UrbanGrowthYear>();
        var currentYear = DateTime.UtcNow.Year;
        double baseArea = Math.Round(rng.NextDouble() * 200 + 50, 1);

        for (int i = 9; i >= 0; i--)
        {
            var year = currentYear - i;
            var growthPct = Math.Round(rng.NextDouble() * 8, 1);
            baseArea = Math.Round(baseArea * (1 + growthPct / 100), 2);

            data.Add(new UrbanGrowthYear
            {
                Year = year,
                UrbanAreaKm2 = baseArea,
                GrowthPctVsPreviousYear = i == 9 ? 0 : growthPct
            });
        }
        return data;
    }
}
