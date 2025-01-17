using Google.Apis.Auth.OAuth2;
using Google.Apis.Gmail.v1;
using Google.Apis.Gmail.v1.Data;
using Google.Apis.Services;
using Microsoft.Extensions.Options;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Models.Email;
using Newtonsoft.Json;

namespace EmailAnalyzer.Server.Services.Email;

public class GmailConfiguration
{
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public string AuthUri { get; set; } = string.Empty;
    public string TokenUri { get; set; } = string.Empty;
    public string[] Scopes { get; set; } = Array.Empty<string>();
}

public class GmailEmailService : IEmailService
{
    private readonly GmailConfiguration _config;
    private readonly ILogger<GmailEmailService> _logger;
    private GmailService? _gmailService;
    private ServerTokenStorageService _tokenStorageService;

    public GmailEmailService(
        IOptions<GmailConfiguration> config,
        ILogger<GmailEmailService> logger, ServerTokenStorageService tokenStorageService)
    {
        _config = config.Value;
        _logger = logger;
        _tokenStorageService = tokenStorageService;

        _logger.LogInformation("GmailEmailService initialized with client ID: {ClientId}",
            _config.ClientId);
    }

    public Task<string> GetAutorizationUrlAsync()
    {
        try
        {
            var clientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            };

            // Create the code flow
            var codeFlow = new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(
                new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                    Scopes = _config.Scopes
                });

            // Generate base URL
            var request = codeFlow.CreateAuthorizationCodeRequest(_config.RedirectUri);

            var url = request.Build();
            var uriBuilder = new UriBuilder(url.AbsoluteUri);
            var query = System.Web.HttpUtility.ParseQueryString(uriBuilder.Query);
            query["access_type"] = "offline";
            query["state"] = "gmail"; // Identyfikator providera
            uriBuilder.Query = query.ToString();

            _logger.LogInformation("Generated Gmail authorization URL");
            return Task.FromResult(uriBuilder.Uri.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate Gmail authorization URL");
            throw;
        }
    }

    public async Task<AuthResponse> AuthenticateAsync(string authCode)
    {
        try
        {
            _logger.LogInformation("Attempting to authenticate with Gmail authorization code");

            var clientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            };

            // Exchange the auth code for tokens
            var codeFlow = new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(
                new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                    Scopes = _config.Scopes
                });

            var token = await codeFlow.ExchangeCodeForTokenAsync(
                "user",
                authCode,
                _config.RedirectUri,
                CancellationToken.None);

            // Initialize Gmail service
            var credential = GoogleCredential.FromAccessToken(token.AccessToken);

            _gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EmailAnalyzer"
            });

            _logger.LogInformation("Successfully authenticated with Gmail");

            return new AuthResponse
            {
                Success = true,
                AccessToken = token.AccessToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(token.ExpiresInSeconds ?? 3600)
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gmail authentication failed");
            return new AuthResponse { Success = false, Error = ex.Message };
        }
    }

    public async Task<List<EmailMessage>> GetEmailsByDateAsync(DateTime startDate, DateTime endDate)
    {
        // Download auth token from saved file
        var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync("gmail");
        if (string.IsNullOrEmpty(accessToken) || expiresAt <= DateTime.UtcNow)
        {
            _logger.LogWarning("Gmail token not found or expired. Please authenticate first.");
            return new List<EmailMessage>();
        }
        
        var credentials = GoogleCredential.FromAccessToken(accessToken); // Initialize Gmail service if not set
        _gmailService = new GmailService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credentials,
            ApplicationName = "EmailAnalyzer"
        });
        
        if (_gmailService == null)
        {
            _logger.LogWarning("Gmail service not initialized. Authentication required.");
            return new List<EmailMessage>();
        }

        try
        {
            _logger.LogInformation("Fetching Gmail messages for date range: {StartDate} to {EndDate}",
                startDate, endDate);

            var query = $"after:{startDate:yyyy/MM/dd} before:{endDate:yyyy/MM/dd}";
            var request = _gmailService.Users.Messages.List("me");
            request.Q = query;
            request.MaxResults = 50;

            var messages = new List<EmailMessage>();
            var response = await request.ExecuteAsync();

            if (response.Messages != null)
            {
                foreach (var message in response.Messages)
                {
                    var emailDetail = await _gmailService.Users.Messages.Get("me", message.Id)
                        .ExecuteAsync();

                    var headers = emailDetail.Payload.Headers;

                    messages.Add(new EmailMessage
                    {
                        Id = emailDetail.Id,
                        Subject = headers.FirstOrDefault(h => h.Name == "Subject")?.Value ?? "[No Subject]",
                        From = headers.FirstOrDefault(h => h.Name == "From")?.Value ?? "unknown@email.com",
                        ReceivedDate = DateTimeOffset.FromUnixTimeMilliseconds(
                            Convert.ToInt64(emailDetail.InternalDate)).DateTime,
                        Preview = emailDetail.Snippet ?? "",
                        Source = "gmail"
                    });
                }
            }

            _logger.LogInformation("Successfully fetched {Count} Gmail messages", messages.Count);
            return messages;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching Gmail messages");
            throw;
        }
    }

    public async Task<bool> RefreshTokenAsync(UserCredentials credentials)
    {
        try
        {
            _logger.LogInformation("Attempting to refresh Gmail token for user: {UserId}",
                credentials.UserId);

            var clientSecrets = new ClientSecrets
            {
                ClientId = _config.ClientId,
                ClientSecret = _config.ClientSecret
            };

            var codeFlow = new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow(
                new Google.Apis.Auth.OAuth2.Flows.GoogleAuthorizationCodeFlow.Initializer
                {
                    ClientSecrets = clientSecrets,
                    Scopes = _config.Scopes
                });

            var token = await codeFlow.RefreshTokenAsync(
                "user",
                credentials.RefreshToken,
                CancellationToken.None);

            if (token == null)
            {
                _logger.LogWarning("Failed to refresh Gmail token");
                return false;
            }

            // Update Gmail service with new token
            var credential = GoogleCredential.FromAccessToken(token.AccessToken);
            _gmailService = new GmailService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EmailAnalyzer"
            });

            _logger.LogInformation("Successfully refreshed Gmail token");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh Gmail token");
            return false;
        }
    }
}