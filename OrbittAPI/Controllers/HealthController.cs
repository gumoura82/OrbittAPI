using Microsoft.AspNetCore.Mvc;
using OrbittAPI.Infrastructure.Data;

namespace OrbittAPI.Controllers;

[Route("health")]
[ApiController]
public class HealthController : ControllerBase
{
    private readonly OrbittDbContext _db;

    public HealthController(OrbittDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Health()
    {
        try
        {
            var canConnect = await _db.Database.CanConnectAsync();
            return Ok(new
            {
                status = "healthy",
                database = canConnect ? "connected" : "unreachable",
                timestamp = DateTime.UtcNow.ToString("O"),
                version = "1.0.0"
            });
        }
        catch (Exception ex)
        {
            return StatusCode(503, new
            {
                status = "unhealthy",
                error = ex.Message,
                timestamp = DateTime.UtcNow.ToString("O")
            });
        }
    }
}
