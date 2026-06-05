using OrbittAPI.Domain.Entities;

namespace OrbittAPI.Domain.Interfaces;

/// <summary>
/// Contrato para repositório de usuários.
/// Interfaces garantem desacoplamento entre camadas (abstração).
/// </summary>
public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByEmailAsync(string email);
    Task<User> CreateAsync(User user);
    Task<User> UpdateAsync(User user);
    Task DeleteAsync(Guid id);
}

/// <summary>
/// Contrato para repositório de API Keys.
/// </summary>
public interface IApiKeyRepository
{
    Task<ApiKey?> GetByValueAsync(string keyValue);
    Task<IEnumerable<ApiKey>> GetByUserIdAsync(Guid userId);
    Task<ApiKey> CreateAsync(ApiKey apiKey);
    Task<ApiKey> UpdateAsync(ApiKey apiKey);
}

/// <summary>
/// Contrato para repositório de chamadas de API (billing/histórico).
/// </summary>
public interface IApiCallRepository
{
    Task<ApiCall> RecordAsync(ApiCall call);
    Task<int> GetMonthlyCountAsync(Guid userId, int year, int month);
    Task<IEnumerable<ApiCall>> GetRecentByUserAsync(Guid userId, int limit = 100);
    Task<IEnumerable<ApiCall>> GetByUserAndPeriodAsync(Guid userId, DateTime from, DateTime to);
}

/// <summary>
/// Contrato para o serviço de processamento de dados satelitais.
/// Permite mock em testes e troca futura por pipeline real.
/// </summary>
public interface ISatelliteDataService
{
    Task<LandUseData> GetLandUseAsync(double lat, double lng);
    Task<VegetationData> GetVegetationAsync(double lat, double lng);
    Task<FloodRiskData> GetFloodRiskAsync(double lat, double lng);
    Task<DeforestationData> GetDeforestationAsync(double lat, double lng, DateTime from, DateTime to);
    Task<UrbanGrowthData> GetUrbanGrowthAsync(double lat, double lng);
}

/// <summary>
/// Contrato para autenticação JWT.
/// </summary>
public interface IAuthService
{
    string GenerateJwtToken(User user);
    Guid? ValidateToken(string token);
}
