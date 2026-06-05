namespace OrbittAPI.Domain.Enums;

public enum SubscriptionPlan
{
    Free = 0,
    Startup = 1,
    Business = 2,
    Enterprise = 3
}

public enum ApiKeyStatus
{
    Active = 0,
    Revoked = 1,
    Expired = 2
}

public enum LandUseType
{
    Vegetation,
    Urban,
    Water,
    ExposedSoil,
    Agriculture
}

public enum AlertSeverity
{
    Low,
    Medium,
    High,
    Critical
}
