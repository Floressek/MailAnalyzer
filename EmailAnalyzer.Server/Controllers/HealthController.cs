using Microsoft.AspNetCore.Mvc;
using EmailAnalyzer.Server.Services.Database;

namespace EmailAnalyzer.Server.Controllers;

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