using Microsoft.AspNetCore.Mvc;
using OrbittAPI.Exceptions;

namespace OrbittAPI.Controllers;

/// <summary>
/// Controller base da OrbittAPI.
/// Fornece helper para extrair o UserId injetado pelo ApiKeyMiddleware.
/// </summary>
[ApiController]
public abstract class OrbittBaseController : ControllerBase
{
    protected Guid GetCurrentUserId()
    {
        if (HttpContext.Items.TryGetValue("UserId", out var value) && value is Guid userId)
            return userId;

        throw new UnauthorizedException("Usuário não identificado. Verifique o header X-Api-Key.");
    }
}
