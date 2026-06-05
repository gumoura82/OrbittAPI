using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OrbittAPI.Domain.Entities;
using OrbittAPI.Domain.Interfaces;

namespace OrbittAPI.Application.Services;

public class AuthService : IAuthService
{
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;

    public AuthService(IConfiguration config, ILogger<AuthService> logger)
    {
        _config = config;
        _logger = logger;
    }

    public string GenerateJwtToken(User user)
    {
        var jwtKey = _config["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key não configurada.");
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim("name", user.Name),
            new Claim("plan", user.Plan.ToString()),
            new Claim(JwtRegisteredClaimNames.Iat,
                DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: creds
        );

        _logger.LogInformation("[Auth] Token JWT gerado para usuário {UserId}", user.Id);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public Guid? ValidateToken(string token)
    {
        try
        {
            var jwtKey = _config["Jwt:Key"];
            if (jwtKey == null) return null;
            var handler = new JwtSecurityTokenHandler();
            var parameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };

            var principal = handler.ValidateToken(token, parameters, out _);
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                       ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return Guid.TryParse(idClaim, out var id) ? id : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("[Auth] Token inválido: {Message}", ex.Message);
            return null;
        }
    }
}
