using Microsoft.AspNetCore.Mvc;

namespace EmailAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    private readonly ILogger<HealthController> _logger;

    public HealthController(ILogger<HealthController> logger)
    {
        _logger = logger;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var port = Environment.GetEnvironmentVariable("PORT") ?? "Not set";
        var env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Not set";
        
        _logger.LogInformation("Health check requested. PORT: {Port}, ENV: {Env}", port, env);
        
        return Ok(new { 
            status = "healthy",
            timestamp = DateTime.UtcNow,
            environment = env,
            port = port
        });
    }
}