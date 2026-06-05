using System.ComponentModel.DataAnnotations;
using OrbittAPI.Domain.Enums;

namespace OrbittAPI.Application.DTOs;

// --- Auth ---

public record RegisterRequest(
    [Required, MaxLength(200)] string Name,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password
);

public record LoginRequest(
    [Required, EmailAddress] string Email,
    [Required] string Password
);

public record LoginResponse(string Token, string ApiKey, string Plan, DateTime ExpiresAt);

public record RegisterResponse(Guid UserId, string Email, string ApiKey, string Message);

// --- Satellite endpoints ---

public record CoordinateRequest(
    [Range(-90, 90)] double Lat,
    [Range(-180, 180)] double Lng
);

public record DeforestationRequest(
    [Range(-90, 90)] double Lat,
    [Range(-180, 180)] double Lng,
    DateTime From,
    DateTime To
);

// --- Dashboard ---

public record UsageStats(
    int CurrentMonthCalls,
    int MonthlyLimit,
    double UsagePercent,
    bool IsNearLimit,
    string Plan
);

public record ApiCallSummary(
    DateTime CalledAt,
    string Endpoint,
    double Latitude,
    double Longitude,
    int HttpStatusCode,
    long ResponseTimeMs,
    string BrasiliaTimestamp
);

// --- Plans ---

public record PlanInfo(
    string Name,
    int MonthlyCallLimit,
    string Price,
    List<string> Features
);

// --- Admin ---

public record RevokeKeyRequest(
    [Required] string ApiKey,
    [Required] string Reason
);

// --- Upgrade ---

public record UpgradePlanRequest(
    [Required] SubscriptionPlan NewPlan
);
