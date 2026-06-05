using Microsoft.AspNetCore.Mvc;
using OrbittAPI.Application.DTOs;
using OrbittAPI.Application.Services;
using OrbittAPI.Domain.Entities;
using OrbittAPI.Domain.Interfaces;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Controllers;

/// <summary>
/// Controller de autenticação.
/// Cobre US-01 (cadastro + API Key), US-02 (login JWT), US-04 (MFA).
/// </summary>
[Route("api/auth")]
public class AuthController : OrbittBaseController
{
    private readonly IUserRepository _users;
    private readonly IApiKeyRepository _apiKeys;
    private readonly IAuthService _auth;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IUserRepository users, IApiKeyRepository apiKeys,
        IAuthService auth, ILogger<AuthController> logger)
    {
        _users = users;
        _apiKeys = apiKeys;
        _auth = auth;
        _logger = logger;
    }

    /// <summary>
    /// US-01 — Cadastro de novo desenvolvedor com geração automática de API Key.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(RegisterResponse), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!ModelState.IsValid)
            throw new BusinessException("Dados de cadastro inválidos. Verifique nome, e-mail e senha (mín. 8 chars).");

        // Verifica se e-mail já existe
        var existing = await _users.GetByEmailAsync(request.Email);
        if (existing != null)
            throw new BusinessException($"O e-mail '{request.Email}' já está cadastrado.");

        // Hash da senha com BCrypt
        var hash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        var user = new User(request.Name, request.Email, hash);
        await _users.CreateAsync(user);

        // Gera API Key automaticamente
        var apiKey = new ApiKey(user.Id);
        await _apiKeys.CreateAsync(apiKey);

        _logger.LogInformation("[Register] Novo usuário criado: {Email}", user.Email);

        return CreatedAtAction(nameof(Register), new RegisterResponse(
            UserId: user.Id,
            Email: user.Email,
            ApiKey: apiKey.KeyValue,
            Message: "Cadastro realizado com sucesso! Guarde sua API Key — ela não será exibida novamente."
        ));
    }

    /// <summary>
    /// US-02 — Login com e-mail e senha. Retorna JWT válido por 24h.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
            throw new BusinessException("E-mail ou senha inválidos.");

        var user = await _users.GetByEmailAsync(request.Email);

        // Verifica usuário e senha com BCrypt
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedException("E-mail ou senha incorretos.");

        if (!user.IsActive)
            throw new UnauthorizedException("Conta desativada. Entre em contato com o suporte.");

        var token = _auth.GenerateJwtToken(user);
        _logger.LogInformation("[Login] Usuário autenticado: {Email}", user.Email);

        return Ok(new LoginResponse(
            Token: token,
            ApiKey: "(Use a API Key gerada no cadastro ou via GET /api/auth/keys)",
            Plan: user.Plan.ToString(),
            ExpiresAt: DateTime.UtcNow.AddHours(24)
        ));
    }

    /// <summary>
    /// Retorna as API Keys ativas do usuário autenticado.
    /// </summary>
    [HttpGet("keys")]
    [ProducesResponseType(200)]
    public async Task<IActionResult> GetMyKeys()
    {
        var userId = GetCurrentUserId();
        var keys = await _apiKeys.GetByUserIdAsync(userId);

        var result = keys.Select(k => new
        {
            k.Id,
            KeyPreview = MaskKey(k.KeyValue),
            k.Status,
            k.CreatedAt,
            k.RevokedAt
        });

        return Ok(result);
    }

    private static string MaskKey(string key)
    {
        if (key.Length <= 12) return "***";
        return key[..8] + "..." + key[^4..];
    }
}
