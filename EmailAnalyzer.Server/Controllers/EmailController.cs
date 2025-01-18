using System.Data;
using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Shared.Models;
using EmailAnalyzer.Shared.Services;
using EmailAnalyzer.Shared.Models.Email;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;

namespace EmailAnalyzer.Server.Controllers;

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

    /// <summary>
    /// This module is used to test the connection to the specified provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
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

    /// <summary>
    /// This module is used to get the available date range for the specified provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    [HttpGet("available-range/{provider}")]
    public async Task<ActionResult<DateRangeInfo>> GetAvailableDateRange(string provider)
    {
        try
        {
            var (accessToken, _, expiresAt) =
                await _tokenStorageService.GetTokenAsync(provider); // _ is used to discard the refresh token
            if (accessToken == null)
            {
                return Unauthorized("No token found. Please authenticate first");
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                return Unauthorized("Token expired. Please re-authenticate");
            }

            return Ok(new DateRangeInfo
            {
                EarliestDate = DateTime.Today.AddMonths(-6), // FIXEME: CONSTRAINTS FOR THE AVAIABLE DATES
                LatestDate = DateTime.Today,
                DefaultStartDate = DateTime.Today.AddMonths(-1), // FIXEME: CONSTRAINTS FOR THE AVAIABLE DATES
                DefaultEndDate = DateTime.Today,
                Provider = provider
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting date range for {Provider}", provider);
            return StatusCode(500, "Error fetching date range");
        }
    }
}