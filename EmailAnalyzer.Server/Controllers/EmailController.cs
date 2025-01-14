using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Shared.Services;
using EmailAnalyzer.Shared.Models.Email;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly ITokenStorageService _tokenStorageService;
    private readonly ILogger<EmailController> _logger;

    public EmailController(
        IEmailServiceFactory emailServiceFactory,
        ITokenStorageService tokenStorageService,
        ILogger<EmailController> logger)
    {
        _emailServiceFactory = emailServiceFactory;
        _tokenStorageService = tokenStorageService;
        _logger = logger;
    }

    [HttpGet("test/{provider}")]
    public async Task<IActionResult> TestConnection(string provider)
    {
        var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
        if (accessToken == null)
        {
            return BadRequest(new { error = "No token found. Please authenticate first" });
        }

        return Ok(new
        {
            message = "Token found",
            expiresAt,
            hasRefreshToken = !string.IsNullOrEmpty(refreshToken) // check if refresh token is available
        });
    }

    /// <summary>
    /// Get emails from the specified provider between the specified dates.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    [HttpPost("{provider}")]
    public async Task<ActionResult<List<EmailMessage>>> GetEmails(
        string provider,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)

    {
        try
        {
            var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
            if (accessToken == null)
            {
                return Unauthorized("No token found. Please authenticate first");
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                return Unauthorized("Token expired. Please re-authenticate");
            }

            var service = _emailServiceFactory.GetService(provider);
            var emails = await service.GetEmailsByDateAsync(startDate, endDate);
            return Ok(emails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emails for {Provider}", provider);
            return StatusCode(500, "Error fetching emails");
        }
    }
}