using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Middleware;

/// <summary>
/// Middleware global de tratamento de exceções.
/// Garante que a aplicação nunca quebre abruptamente e retorna erros no formato RFC 7807.
/// Requisito: Tratamento de Exceções (10 pontos)
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning("[404] {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.NotFound, "Not Found", ex.Message);
        }
        catch (BusinessException ex)
        {
            _logger.LogWarning("[400] {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Bad Request", ex.Message);
        }
        catch (UnauthorizedException ex)
        {
            _logger.LogWarning("[401] {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.Unauthorized, "Unauthorized", ex.Message);
        }
        catch (ForbiddenException ex)
        {
            _logger.LogWarning("[403] {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.Forbidden, "Forbidden", ex.Message);
        }
        catch (QuotaExceededException ex)
        {
            _logger.LogWarning("[429] Quota exceeded. Limit={Limit} Current={Current}", ex.Limit, ex.CurrentUsage);
            await WriteErrorAsync(context, HttpStatusCode.TooManyRequests, "Too Many Requests", ex.Message);
        }
        catch (InvalidCoordinatesException ex)
        {
            _logger.LogWarning("[400] Invalid coordinates: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Invalid Coordinates", ex.Message);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            _logger.LogWarning("[400] Argument out of range: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Validation Error", ex.Message);
        }
        catch (DbUpdateException ex)
        {
            // Falha ao persistir no banco — pode ser violação de constraint, FK órfã, etc.
            _logger.LogError(ex, "[500] Database update error: {Message}", ex.InnerException?.Message ?? ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                "Database Error",
                "Não foi possível persistir os dados. Verifique se os campos estão corretos e tente novamente.");
        }
        catch (FormatException ex)
        {
            _logger.LogWarning("[400] Format error: {Message}", ex.Message);
            await WriteErrorAsync(context, HttpStatusCode.BadRequest, "Format Error", $"Formato inválido: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[500] Unhandled exception at {Path}", context.Request.Path);
            await WriteErrorAsync(context, HttpStatusCode.InternalServerError,
                "Internal Server Error",
                "Ocorreu um erro interno. Tente novamente ou contate o suporte.");
        }
    }

    /// <summary>
    /// Formata o erro no padrão RFC 7807 (Problem Details for HTTP APIs).
    /// </summary>
    private static async Task WriteErrorAsync(HttpContext context, HttpStatusCode statusCode,
        string title, string detail)
    {
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new
        {
            type = $"https://httpstatuses.io/{(int)statusCode}",
            title,
            status = (int)statusCode,
            detail,
            instance = context.Request.Path.Value,
            timestamp = DateTime.UtcNow.ToString("O")
        };

        var json = JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}
