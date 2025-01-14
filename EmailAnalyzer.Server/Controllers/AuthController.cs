using Microsoft.AspNetCore.Mvc;
using EmailAnalyzer.Shared.Models.Auth;
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
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IEmailServiceFactory emailServiceFactory,
        ILogger<AuthController> logger)
    {
        _emailServiceFactory = emailServiceFactory;
        _logger = logger;
    }
    
    [HttpGet("url/{provider}")]
    public async Task<IActionResult> GetAuthUrl(string provider)
    {
        try
        {
            var service = _emailServiceFactory.GetService(provider);
            var url = await service.GetAutorizationUrlAsync();
            return Ok(new { url });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting auth URL for {Provider}", provider);
            return BadRequest(new { error = "Could not generate auth URL" });
        }
    }
    
    [HttpPost("authenticate")]
    public async Task<ActionResult<AuthResponse>> Authenticate([FromBody] AuthRequest request)
    {
        try
        {
            var service = _emailServiceFactory.GetService(request.Provider);
            var response = await service.AuthenticateAsync(request.AuthCode);

            if (!response.Success)
            {
                return BadRequest(response);
            }

            // TODO: Store user credentials in database

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for {Provider}", request.Provider);
            return BadRequest(new AuthResponse
            {
                Success = false,
                Error = "Authentication failed"
            });
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
            return Content($"<html><body><h1>Error during authentication: {ex.Message}</h1></body></html>", "text/html");
        }
    }
}