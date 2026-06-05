using System.Text;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Domain;

/// <summary>
/// Classe estática utilitária para validação e manipulação de coordenadas geográficas.
/// Centraliza as regras usadas por todos os endpoints satelitais.
/// </summary>
public static class CoordinateValidator
{
    public const double MinLatitude  = -90.0;
    public const double MaxLatitude  =  90.0;
    public const double MinLongitude = -180.0;
    public const double MaxLongitude =  180.0;

    public static void Validate(double lat, double lng)
    {
        if (lat < MinLatitude || lat > MaxLatitude || lng < MinLongitude || lng > MaxLongitude)
            throw new InvalidCoordinatesException(lat, lng);
    }

    public static void ThrowIfInvalid(double lat, double lng) => Validate(lat, lng);

    public static bool IsValid(double lat, double lng)
        => lat >= MinLatitude && lat <= MaxLatitude
        && lng >= MinLongitude && lng <= MaxLongitude;

    /// <summary>
    /// Distância aproximada entre dois pontos em km (fórmula de Haversine).
    /// </summary>
    public static double DistanceKm(double lat1, double lng1, double lat2, double lng2)
    {
        const double R = 6371.0;
        var dLat = ToRad(lat2 - lat1);
        var dLng = ToRad(lng2 - lng1);
        var a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
              + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
              * Math.Sin(dLng / 2) * Math.Sin(dLng / 2);
        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        return Math.Round(R * c, 2);
    }

    public static bool IsWithinBoundingBox(double lat, double lng,
        double minLat, double maxLat, double minLng, double maxLng)
        => lat >= minLat && lat <= maxLat && lng >= minLng && lng <= maxLng;

    public static bool IsInBrazil(double lat, double lng)
        => IsWithinBoundingBox(lat, lng, -33.75, 5.27, -73.99, -28.85);

    /// <summary>
    /// Formata coordenadas no padrão DMS (Graus, Minutos, Segundos).
    /// Demonstra uso de for clássico para construção de string formatada.
    /// </summary>
    public static string FormatDms(double lat, double lng)
    {
        var parts = new (double value, string pos, string neg)[2]
        {
            (lat, "N", "S"),
            (lng, "E", "W")
        };

        var result = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            var (value, pos, neg) = parts[i];
            var abs     = Math.Abs(value);
            var degrees = (int)abs;
            var minutes = (int)((abs - degrees) * 60);
            var seconds = Math.Round(((abs - degrees) * 60 - minutes) * 60, 1);
            var dir     = value >= 0 ? pos : neg;

            if (i > 0) result.Append(", ");
            result.Append($"{degrees}°{minutes}'{seconds}\"{dir}");
        }

        return result.ToString();
    }

    private static double ToRad(double degrees) => degrees * Math.PI / 180.0;
}

/// <summary>
/// Configurações de limites por plano de assinatura.
/// </summary>
public static class PlanLimitConfig
{
    public static readonly IReadOnlyDictionary<string, int> MonthlyCallLimits =
        new Dictionary<string, int>
        {
            { "Free",       500       },
            { "Startup",    10_000    },
            { "Business",   100_000   },
            { "Enterprise", int.MaxValue }
        };

    public static readonly IReadOnlyDictionary<string, string> PlanPrices =
        new Dictionary<string, string>
        {
            { "Free",       "Grátis"      },
            { "Startup",    "R$ 99/mês"   },
            { "Business",   "R$ 499/mês"  },
            { "Enterprise", "Consultar"   }
        };

    public static List<string> GetPlanSummaries()
    {
        var plans = MonthlyCallLimits.Keys.ToArray();
        var summaries = new List<string>();

        for (int i = 0; i < plans.Length; i++)
        {
            var plan = plans[i];
            var limit = MonthlyCallLimits[plan] == int.MaxValue ? "Ilimitado" : MonthlyCallLimits[plan].ToString("N0");
            summaries.Add($"[{i + 1}] {plan}: {limit} chamadas/mês — {PlanPrices[plan]}");
        }

        return summaries;
    }
}
