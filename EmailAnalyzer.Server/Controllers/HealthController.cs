using Microsoft.AspNetCore.Mvc;

namespace EmailAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        Console.WriteLine("Health check requested");
        return Ok(new { status = "Server is running" });
    }
}