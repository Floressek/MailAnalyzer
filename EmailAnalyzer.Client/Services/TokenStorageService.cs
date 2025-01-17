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
                AccessToken = accessToken,  // Dodaj też AccessToken
                RefreshToken = refreshToken,
                TokenExpiry = expiresAt
            };
            
            var json = JsonSerializer.Serialize(credentials);
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

            var credentials = JsonSerializer.Deserialize<UserCredentials>(json);
            if (credentials == null)
            {
                return (null, null, DateTime.MinValue);
            }

            return (credentials.AccessToken, credentials.RefreshToken, credentials.TokenExpiry);  // Zwróć też AccessToken
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
            await Task.CompletedTask;  // Dodaj to, żeby metoda była rzeczywiście asynchroniczna
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove tokens for {Provider}", provider);
            throw;
        }
    }
}