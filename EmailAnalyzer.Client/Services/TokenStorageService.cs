using Microsoft.Extensions.Logging;
using System.Text.Json;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services; 

namespace EmailAnalyzer.Client.Services;

public class SecureTokenStorageService : ITokenStorageService
{
    private readonly ILogger<SecureTokenStorageService> _logger;

    public SecureTokenStorageService(ILogger<SecureTokenStorageService> logger)
    {
        _logger = logger;
    }

    public async Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt)
    {
        try
        {
            var credentials = new UserCredentials
            {
                Provider = provider,
                RefreshToken = refreshToken,
                TokenExpiry = expiresAt
            };
            
            var json = JsonSerializer.Serialize(credentials); // Changed from JsonConvert
            await SecureStorage.Default.SetAsync($"token_{provider}", json);
            
            _logger.LogInformation("Token stored for {Provider}", provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing token for {Provider}", provider);
            throw;
        }
    }
    
    public async Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider)
    {
        try
        {
            var json = await SecureStorage.Default.GetAsync($"token_{provider}");
            if (string.IsNullOrEmpty(json))
            {
                _logger.LogWarning("Token not found for {Provider}", provider);
                return (null, null, DateTime.MinValue);
            }

        
            // var tokenData = JsonSerializer.Deserialize<dynamic>(json); // Changed from JsonConvert
            // return (
            //     tokenData.AccessToken.ToString(),
            //     tokenData.RefreshToken.ToString(),
            //     DateTime.Parse(tokenData.ExpiresAt.ToString())
            // );
            
            var credentials = JsonSerializer.Deserialize<UserCredentials>(json);
            if (credentials == null)
            {
                return (null, null, DateTime.MinValue);
            }

            return (null, credentials.RefreshToken, credentials.TokenExpiry);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving token for {Provider}", provider);
            return (null, null, DateTime.MinValue);
        }
    }
    
    public async Task RemoveTokenAsync(string provider)
    {
        try
        {
            SecureStorage.Default.Remove($"token_{provider}");
            _logger.LogInformation("Tokens removed for {Provider}", provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tokens for {Provider}", provider);
            throw;
        }
    }
}