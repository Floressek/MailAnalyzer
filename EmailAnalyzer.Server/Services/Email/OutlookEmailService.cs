using Microsoft.Graph;
using Azure.Identity;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Models.Email;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client;
using System.Net.Http.Headers;
using Microsoft.Kiota.Abstractions;
using Microsoft.Kiota.Abstractions.Authentication;
using MongoDB.Driver.Core.Authentication;

namespace EmailAnalyzer.Server.Services.Email;

public class OutlookConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string Authority { get; set; } = string.Empty; // Z TenantId na Authority
    public string Endpoint { get; set; } = string.Empty; // Added
    public string[] Scopes { get; set; } = Array.Empty<string>();
}

/// <summary>
/// This interface represents the email service.
/// </summary>
public class OutlookEmailService : IEmailService
{
    private readonly OutlookConfiguration _config;

    private readonly ILogger<OutlookEmailService> _logger;

    private readonly IConfidentialClientApplication _msalClient;
    // private readonly IPublicClientApplication _msalClient;
    private GraphServiceClient? _graphClient;
    private ServerTokenStorageService _tokenStorageService;

    /// <summary>
    /// This constructor initializes the email service. FOR OUTLOOK.
    /// </summary>
    /// <param name="config"></param>
    /// <param name="logger"></param>
    public OutlookEmailService(
        IOptions<OutlookConfiguration> config,
        ILogger<OutlookEmailService> logger, 
        ServerTokenStorageService tokenStorageService)
    
    {
        _config = config.Value;
        _logger = logger;
        _tokenStorageService = tokenStorageService;

        // Initialize MSAL client
        _msalClient = ConfidentialClientApplicationBuilder
            .Create(_config.ClientId)
            .WithClientSecret(_config.ClientSecret)
            .WithRedirectUri(_config.RedirectUri)
            // .WithAuthority(AzureCloudInstance.AzurePublic, _config.TenantId)
            .WithAuthority(_config.Authority)
            .Build();

        // _msalClient = PublicClientApplicationBuilder
        //     .Create(_config.ClientId)
        //     .WithAuthority(_config.Authority)
        //     .WithRedirectUri(_config.RedirectUri)
        //     .Build();
        _logger.LogInformation("OutlookEmailService initialized with client ID: {ClientId}", _config.ClientId);
    }

    /// <summary>
    /// This method retrives the url for authorization.
    /// </summary>
    /// <returns>AbsoluteUrl</returns>
    public async Task<string> GetAutorizationUrlAsync()
    {
        try
        {
            var url = await _msalClient.GetAuthorizationRequestUrl(_config.Scopes)
                .WithRedirectUri(_config.RedirectUri)
                .WithPrompt(Prompt.SelectAccount)
                .WithExtraQueryParameters(new Dictionary<string, string> { { "state", "outlook" } })
                .ExecuteAsync();
            

            _logger.LogInformation("Generated authorization URL: {Url}", url);
            return url.AbsoluteUri; // Return the URL
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate authorization URL");
            throw;
        }
    }

    /// <summary>
    /// This method authenticates the user with the provided auth code.
    /// </summary>
    /// <param name="authCode"></param>
    /// <returns></returns>
    public async Task<AuthResponse> AuthenticateAsync(string authCode)
    {
        try
        {
            _logger.LogInformation("Attempting to authenticate with auth code: {AuthCode}", authCode);

            var result = await _msalClient.AcquireTokenByAuthorizationCode(
                _config.Scopes,
                authCode
            ).ExecuteAsync();

            // Initialize Graph client with the access token
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", result.AccessToken);
            _graphClient = new GraphServiceClient(httpClient);

            _logger.LogInformation("Successfully authenticated user: {UserEmail}", result.Account?.Username);

            return new AuthResponse
            {
                Success = true,
                AccessToken = result.AccessToken,
                ExpiresAt = result.ExpiresOn.DateTime,
                // RefreshToken = result.RefreshToken
            };
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "MSAL authentication failed");
            return new AuthResponse { Success = false, Error = ex.Message };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed");
            return new AuthResponse { Success = false, Error = "Authentication failed" };
        }
    }

    public async Task<List<EmailMessage>> GetEmailsByDateAsync(DateTime startDate, DateTime endDate)
    {
        if (_graphClient is null)
        {
            _logger.LogError("Graph client is not initialized. Please authenticate first.");
            return new List<EmailMessage>(); // Return empty list
        }

        // Sprawdź token przed próbą pobrania maili
        var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync("outlook");
        if (string.IsNullOrEmpty(accessToken) || expiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Outlook token not found or expired. Please authenticate first.");
            return new List<EmailMessage>();
        }
        
        var httpClient = new HttpClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        _graphClient = new GraphServiceClient(httpClient);
        
        try
        {
            _logger.LogInformation("Retrieving emails from {StartDate} to {EndDate}", startDate, endDate);


            var messages = await _graphClient.Me.Messages
                .GetAsync(requestConfiguration =>
                {
                    requestConfiguration.QueryParameters.Filter =
                        $"receivedDateTime ge {startDate:yyyy-MM-ddTHH:mm:ssZ} and receivedDateTime le {endDate:yyyy-MM-ddTHH:mm:ssZ}";
                    requestConfiguration.QueryParameters.Select = new[]
                        { "id", "subject", "from", "receivedDateTime", "bodyPreview" }; // Select only these fields
                    requestConfiguration.QueryParameters.Orderby = new[] { "receivedDateTime desc" };
                    requestConfiguration.QueryParameters.Top = 50; // Limit to 50 emails TODO: Implement pagination
                });

            if (messages?.Value == null)
            {
                _logger.LogWarning("No emails found in the specified date range from Graph API");
                return new List<EmailMessage>();
            }

            var emailMessages = messages.Value.Select(msg => new EmailMessage
            {
                Id = msg.Id ?? Guid.NewGuid().ToString(), // Generate a new ID if null
                Subject = msg.Subject ?? "[No subject]",
                From = msg.From?.EmailAddress?.Address ?? "unknown@email.com",
                ReceivedDate = msg.ReceivedDateTime?.DateTime ?? DateTime.UtcNow,
                Preview = msg.BodyPreview ?? "",
                Source = "outlook"
            }).ToList();

            _logger.LogInformation("Retrieved {Count} emails from Graph API", emailMessages.Count);
            return emailMessages;
        }
        catch (ServiceException ex)
        {
            _logger.LogError(ex, "Microsoft Graph API error");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching emails");
            throw;
        }
    }

    public async Task<bool> RefreshTokenAsync(UserCredentials credentials)
    {
        try
        {
            var accounts = await _msalClient.GetAccountsAsync();
            var account = accounts.FirstOrDefault();

            if (account is null)
            {
                _logger.LogError("No accounts found for token refresh");
                return false;
            }

            var result = await _msalClient.AcquireTokenSilent(_config.Scopes, account)
                .ExecuteAsync();

            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", result.AccessToken);
            _graphClient = new GraphServiceClient(httpClient);

            _logger.LogInformation("Successfully refreshed token for user: {UserEmail}", account.Username);
            return true;
        }
        catch (MsalUiRequiredException ex)
        {
            _logger.LogError(ex, "Interactive authentication required for token refresh");
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh token");
            return false;
        }
    }
}