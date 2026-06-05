using OrbittAPI.Domain.Enums;

namespace OrbittAPI.Domain.Entities;

/// <summary>
/// Classe abstrata base para todos os dados satelitais retornados pelos endpoints.
/// Impõe o contrato de serialização e metadados comuns a todas as análises.
/// </summary>
public abstract class SatelliteDataResult
{
    public double Latitude { get; protected set; }
    public double Longitude { get; protected set; }
    public DateTime CapturedAt { get; protected set; }
    public string DataSource { get; protected set; } = string.Empty;
    public AlertSeverity? AlertLevel { get; protected set; }

    protected SatelliteDataResult(double lat, double lng, string source)
    {
        Latitude = lat;
        Longitude = lng;
        DataSource = source;
        CapturedAt = SimulateLastCaptureDate(lat, lng);
    }

    /// <summary>
    /// Método abstrato: cada tipo de dado deve definir sua própria descrição de risco/alerta.
    /// </summary>
    public abstract string GetAlertDescription();

    /// <summary>
    /// Simula a data da última imagem disponível (dados reais viriam do pipeline de ingestão).
    /// Usa seed baseado nas coordenadas para garantir determinismo:
    /// a mesma coordenada sempre retorna a mesma data.
    /// </summary>
    private static DateTime SimulateLastCaptureDate(double lat, double lng)
    {
        var seed = (int)(Math.Abs(lat) * 1000 + Math.Abs(lng) * 100);
        var daysBack = new Random(seed).Next(1, 10);
        return DateTime.UtcNow.AddDays(-daysBack).Date;
    }

    public string FormattedCaptureDate() => CapturedAt.ToString("yyyy-MM-dd");
}

/// <summary>
/// Dados de uso do solo retornados pelo endpoint /landuse.
/// Herda de SatelliteDataResult com especialização para análise de cobertura.
/// </summary>
public class LandUseData : SatelliteDataResult
{
    public double VegetationPct { get; private set; }
    public double UrbanPct { get; private set; }
    public double WaterPct { get; private set; }
    public double ExposedSoilPct { get; private set; }
    public LandUseType DominantType { get; private set; }

    public LandUseData(double lat, double lng, double vegetation, double urban, double water, double soil)
        : base(lat, lng, "Sentinel-2 / ESA Copernicus")
    {
        VegetationPct = vegetation;
        UrbanPct = urban;
        WaterPct = water;
        ExposedSoilPct = soil;
        DominantType = CalculateDominantType();
    }

    public override string GetAlertDescription()
    {
        return DominantType switch
        {
            LandUseType.Urban when UrbanPct > 70 => "Alta densidade urbana detectada.",
            LandUseType.ExposedSoil when ExposedSoilPct > 50 => "Área com solo exposto crítico — risco de erosão.",
            LandUseType.Vegetation => "Cobertura vegetal predominante. Área saudável.",
            LandUseType.Water => "Região predominantemente aquática.",
            _ => "Uso do solo misto. Nenhum alerta crítico."
        };
    }

    private LandUseType CalculateDominantType()
    {
        var values = new Dictionary<LandUseType, double>
        {
            { LandUseType.Vegetation, VegetationPct },
            { LandUseType.Urban, UrbanPct },
            { LandUseType.Water, WaterPct },
            { LandUseType.ExposedSoil, ExposedSoilPct }
        };
        return values.MaxBy(kv => kv.Value).Key;
    }
}

/// <summary>
/// Dados de vegetação (NDVI) retornados pelo endpoint /vegetation.
/// </summary>
public class VegetationData : SatelliteDataResult
{
    public double NdviIndex { get; private set; }
    public string HealthStatus { get; private set; }

    public VegetationData(double lat, double lng, double ndvi)
        : base(lat, lng, "Landsat-9 / NASA + Sentinel-2 / ESA")
    {
        if (ndvi < -1 || ndvi > 1)
            throw new ArgumentOutOfRangeException(nameof(ndvi), "NDVI deve estar entre -1 e 1.");

        NdviIndex = Math.Round(ndvi, 4);
        HealthStatus = ClassifyHealth(ndvi);
        AlertLevel = ndvi < 0.2 ? AlertSeverity.High : AlertSeverity.Low;
    }

    public override string GetAlertDescription()
    {
        return NdviIndex switch
        {
            < 0 => "Solo exposto ou superfície aquática. Vegetação ausente.",
            < 0.2 => "Vegetação esparsa ou degradada. Monitoramento recomendado.",
            < 0.4 => "Vegetação moderada. Condição aceitável.",
            < 0.6 => "Vegetação densa. Área produtiva.",
            _ => "Vegetação muito densa. Floresta ou cultivo irrigado."
        };
    }

    private static string ClassifyHealth(double ndvi) => ndvi switch
    {
        < 0 => "Sem vegetação",
        < 0.2 => "Muito baixa",
        < 0.4 => "Baixa",
        < 0.6 => "Moderada",
        < 0.8 => "Alta",
        _ => "Muito alta"
    };
}

/// <summary>
/// Dados de risco de alagamento retornados pelo endpoint /flood-risk.
/// </summary>
public class FloodRiskData : SatelliteDataResult
{
    public double RiskScore { get; private set; }    // 0 a 10
    public List<FloodHistoryEntry> History { get; private set; }

    public FloodRiskData(double lat, double lng, double riskScore, List<FloodHistoryEntry> history)
        : base(lat, lng, "INPE + Copernicus Emergency Management Service")
    {
        if (riskScore < 0 || riskScore > 10)
            throw new ArgumentOutOfRangeException(nameof(riskScore), "Score deve ser entre 0 e 10.");

        RiskScore = Math.Round(riskScore, 1);
        History = history ?? new List<FloodHistoryEntry>();
        AlertLevel = riskScore >= 7 ? AlertSeverity.Critical
                   : riskScore >= 5 ? AlertSeverity.High
                   : riskScore >= 3 ? AlertSeverity.Medium
                   : AlertSeverity.Low;
    }

    public override string GetAlertDescription()
    {
        return AlertLevel switch
        {
            AlertSeverity.Critical => $"RISCO CRÍTICO de alagamento (score {RiskScore}). Ação imediata necessária.",
            AlertSeverity.High     => $"Risco alto de alagamento (score {RiskScore}). Monitoramento intensivo.",
            AlertSeverity.Medium   => $"Risco moderado (score {RiskScore}). Acompanhar condições climáticas.",
            _                      => $"Risco baixo (score {RiskScore}). Sem alertas ativos."
        };
    }
}

/// <summary>
/// Entrada do histórico de inundações para uma região.
/// </summary>
public class FloodHistoryEntry
{
    public int Year { get; set; }
    public bool FloodOccurred { get; set; }
    public double MaxRiskScore { get; set; }
    public DateTime? FloodDate { get; set; }
}

/// <summary>
/// Dados de desmatamento retornados pelo endpoint /deforestation.
/// </summary>
public class DeforestationData : SatelliteDataResult
{
    public double DeforestedAreaKm2 { get; private set; }
    public DateTime PeriodStart { get; private set; }
    public DateTime PeriodEnd { get; private set; }
    public List<DeforestationAlert> Alerts { get; private set; }

    public DeforestationData(double lat, double lng, double areaKm2, DateTime start, DateTime end, List<DeforestationAlert> alerts)
        : base(lat, lng, "PRODES/INPE + Sentinel-2")
    {
        if (start > end)
            throw new ArgumentException("Data de início não pode ser posterior ao fim do período.");

        DeforestedAreaKm2 = Math.Round(areaKm2, 2);
        PeriodStart = start;
        PeriodEnd = end;
        Alerts = alerts ?? new List<DeforestationAlert>();
        AlertLevel = areaKm2 > 10 ? AlertSeverity.Critical
                   : areaKm2 > 1  ? AlertSeverity.High
                   : AlertSeverity.Medium;
    }

    public override string GetAlertDescription()
    {
        var days = (PeriodEnd - PeriodStart).Days;
        return $"{DeforestedAreaKm2} km² desmatados nos últimos {days} dias. {Alerts.Count} alerta(s) emitido(s).";
    }
}

/// <summary>
/// Alerta pontual de desmatamento com timestamp.
/// </summary>
public class DeforestationAlert
{
    public DateTime DetectedAt { get; set; }
    public double AreaKm2 { get; set; }
    public string? SatelliteImageRef { get; set; }
}

/// <summary>
/// Dados de crescimento urbano retornados pelo endpoint /urban-growth.
/// </summary>
public class UrbanGrowthData : SatelliteDataResult
{
    public List<UrbanGrowthYear> YearlyData { get; private set; }
    public double TotalGrowthPct { get; private set; }

    public UrbanGrowthData(double lat, double lng, List<UrbanGrowthYear> yearlyData)
        : base(lat, lng, "GHSL / Copernicus + Landsat")
    {
        if (yearlyData == null || !yearlyData.Any())
            throw new ArgumentException("Dados anuais não podem ser nulos ou vazios.");

        YearlyData = yearlyData.OrderBy(y => y.Year).ToList();
        TotalGrowthPct = CalculateTotalGrowth();
    }

    public override string GetAlertDescription()
    {
        return TotalGrowthPct switch
        {
            > 100 => "Expansão urbana acelerada. Impacto ambiental significativo esperado.",
            > 50  => "Crescimento urbano relevante. Planejamento urbano necessário.",
            > 20  => "Crescimento moderado e controlado.",
            _     => "Expansão urbana estável nos últimos 10 anos."
        };
    }

    private double CalculateTotalGrowth()
    {
        if (YearlyData.Count < 2) return 0;
        var first = YearlyData.First().UrbanAreaKm2;
        var last = YearlyData.Last().UrbanAreaKm2;
        if (first == 0) return 0;
        return Math.Round((last - first) / first * 100, 1);
    }
}

public class UrbanGrowthYear
{
    public int Year { get; set; }
    public double UrbanAreaKm2 { get; set; }
    public double GrowthPctVsPreviousYear { get; set; }
}
