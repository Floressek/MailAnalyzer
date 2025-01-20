using Microsoft.AspNetCore.Mvc;
using EmailAnalyzer.Server.Services.Database;

namespace EmailAnalyzer.Server.Controllers;

/// <summary>
/// Controller for health monitoring and diagnostics.
/// </summary>
/// <remarks>
/// PL: Kontroler do monitorowania stanu i diagnostyki aplikacji.
/// Używany do sprawdzania stanu usługi i połączeń.
/// </remarks>
[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;
    private readonly MongoDBService _mongoDBService;

    public HealthController(
        ILogger<HealthController> logger,
        MongoDBService mongoDBService)
    {
        _logger = logger;
        _mongoDBService = mongoDBService;
    }

    /// <summary>
    /// Gets the general health status of the application.
    /// </summary>
    /// <remarks>
    /// PL: Pobiera ogólny status zdrowia aplikacji.
    /// Używane w: Monitorowaniu stanu usługi na Railway.
    /// </remarks>
    [HttpGet]
    public IActionResult Get()
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "Not set";
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not set";

        _logger.LogInformation("Health check requested. PORT: {Port}, ENV: {Env}", port, env);

        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = env,
            port = port
        });
    }

    /// <summary>
    /// Checks MongoDB connection health.
    /// </summary>
    /// <remarks>
    /// PL: Sprawdza stan połączenia z MongoDB.
    /// Używane w: Monitorowaniu stanu bazy danych na Railway.
    /// </remarks>
    [HttpGet("mongo")]
    public async Task<ActionResult> CheckMongo()
    {
        try
        {
            var collections = await _mongoDBService.ListCollectionsAsync();
            _logger.LogInformation("MongoDB collections: {Collections}", collections);

            return Ok(new
            {
                status = "healthy",
                databaseName = "emailanalyzer",
                collections = collections,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB health check failed");
            return StatusCode(500, new
            {
                status = "unhealthy",
                error = ex.Message,
                timestamp = DateTime.UtcNow,
                details = ex.ToString()
            });
        }
    }
}