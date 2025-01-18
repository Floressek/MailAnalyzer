using System.Text.Json;
using EmailAnalyzer.Server.Services;
using Microsoft.AspNetCore.Mvc;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services;
using EmailAnalyzer.Server.Services.Email;

namespace EmailAnalyzer.Server.Controllers;

/// <summary>
/// This controller is responsible for handling the authentication process.
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly ITokenStorageService _tokenStorageService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IEmailServiceFactory emailServiceFactory,
        ITokenStorageService tokenStorageService,
        ILogger<AuthController> logger)
    {
        _emailServiceFactory = emailServiceFactory;
        _tokenStorageService = tokenStorageService;
        _logger = logger;
    }

    [HttpGet("url/{provider}")]
    public async Task<IActionResult> GetAuthUrl(string provider)
    {
        try
        {
            Console.WriteLine($"[AUTH CONTROLLER] Received request for provider: {provider}");
            var service = _emailServiceFactory.GetService(provider);
            var url = await service.GetAutorizationUrlAsync();
            Console.WriteLine($"[AUTH CONTROLLER] Generated URL: {url}");
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AUTH CONTROLLER] Error: {ex.Message}\n{ex.StackTrace}");
            _logger.LogError(ex, "Error getting auth URL for {Provider}", provider);
            return BadRequest(new { error = "Could not generate auth URL" });
        }
    }

    [HttpPost("authenticate")]
    public async Task<ActionResult<AuthResponse>> Authenticate([FromBody] AuthRequest request)
    {
        _logger.LogInformation("Received authentication request for provider: {Provider}", request.Provider);

        if (string.IsNullOrEmpty(request.AuthCode))
        {
            _logger.LogWarning("AuthCode is null or empty");
            return BadRequest("AuthCode is required");
        }

        try
        {
            var authResponse = await _emailServiceFactory.GetService(request.Provider)
                .AuthenticateAsync(request.AuthCode);

            if (authResponse.Success)
            {
                _logger.LogInformation("Authentication successful for provider: {Provider}", request.Provider);

                // Store the token
                await _tokenStorageService.StoreTokenAsync(
                    request.Provider,
                    authResponse.AccessToken!,
                    authResponse.RefreshToken ?? "",
                    authResponse.ExpiresAt
                );

                return Ok(new { message = "Authentication successful" });
            }

            _logger.LogError("Authentication failed: {Error}", authResponse.Error);
            return BadRequest(authResponse.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication processing failed");
            return StatusCode(500, "Internal Server Error");
        }
    }

    [Route("~/auth/callback")]
    [HttpGet]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        try
        {
            _logger.LogInformation("Received callback with code length: {CodeLength} and state: {State}",
                code?.Length ?? 0, state);

            state = state?.Trim('"', ' ', '}', '{');

            if (string.IsNullOrEmpty(state))
            {
                _logger.LogError("No state parameter provided in callback");
                return BadRequest("Invalid callback: no provider specified");
            }

            var authRequest = new AuthRequest
            {
                Provider = state.ToLower(),
                AuthCode = code
            };

            _logger.LogInformation("Attempting to authenticate with provider: {Provider}", authRequest.Provider);

            var response = await Authenticate(authRequest);

            _logger.LogInformation("Authentication response: {Response}",
                response.Result is OkObjectResult ? "Success" : "Failure");

            if (response.Result is OkObjectResult okResult)
            {
                return Content(
                    "<html><body><h1>Authentication successful! You can close this window.</h1></body></html>",
                    "text/html");
            }

            // Dodajmy więcej informacji o błędzie
            if (response.Result is BadRequestObjectResult badResult)
            {
                _logger.LogError("Authentication failed with error: {Error}",
                    (badResult.Value as AuthResponse)?.Error ?? "Unknown error");
            }

            return Content("<html><body><h1>Authentication failed!</h1></body></html>", "text/html");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Callback error: {Message}", ex.Message);
            return Content($"<html><body><h1>Error during authentication: {ex.Message}</h1></body></html>",
                "text/html");
        }
    }

    [HttpGet("all-tokens")]
    public IActionResult GetAllTokens()
    {
        try
        {
            var tokens = _tokenStorageService.GetAllTokens();

            if (tokens == null || !tokens.Any())
            {
                _logger.LogWarning("No tokens found to return.");
                return Ok(new { message = "No tokens available" });
            }

            _logger.LogInformation("Returning tokens: {Tokens}", JsonSerializer.Serialize(tokens));

            foreach (var (key, value) in tokens)
            {
                _logger.LogDebug(
                    "Provider={Provider}, AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
                    key, value.AccessToken, value.RefreshToken, value.ExpiresAt);
            }

            return Ok(tokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching all tokens");
            return StatusCode(500, "Internal Server Error");
        }
    }
    
    [HttpDelete("remove-token/{provider}")]
    public async Task<IActionResult> RemoveToken(string provider)
    {
        try
        {
            await _tokenStorageService.RemoveTokenAsync(provider);
            return Ok(new { message = "Token removed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing token for provider: {Provider}", provider);
            return StatusCode(500, "Internal Server Error");
        }
    }
}