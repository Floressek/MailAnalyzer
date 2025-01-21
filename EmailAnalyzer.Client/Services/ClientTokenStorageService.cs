using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Net.Http.Json;

namespace EmailAnalyzer.Client.Services;

public class ClientTokenStorageService : ITokenStorageService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ClientTokenStorageService> _logger;
    private Dictionary<string, TokenData> _cachedTokens = new();

    public ClientTokenStorageService(HttpClient httpClient, ILogger<ClientTokenStorageService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt)
    {
        try
        {
            var tokenData = new TokenData
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = expiresAt
            };

            _logger.LogInformation("Storing token for provider: {Provider}", provider);
            var response = await _httpClient.PostAsJsonAsync($"api/auth/store-token/{provider}", tokenData);
            
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Token successfully stored for provider: {Provider}", provider);
            
            // Update cache
            _cachedTokens[provider] = tokenData;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store token for provider: {Provider}", provider);
            throw;
        }
    }

    public async Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider)
    {
        try
        {
            _logger.LogInformation("Getting token for provider: {Provider}", provider);
            var response = await _httpClient.GetAsync("api/auth/all-tokens");
            response.EnsureSuccessStatusCode();

            var tokens = await response.Content.ReadFromJsonAsync<Dictionary<string, TokenData>>();
            _cachedTokens = tokens ?? new Dictionary<string, TokenData>();
            
            if (_cachedTokens.TryGetValue(provider, out var token))
            {
                _logger.LogInformation("Token found for provider: {Provider}", provider);
                return (token.AccessToken, token.RefreshToken, token.ExpiresAt);
            }

            _logger.LogWarning("Token not found for provider: {Provider}", provider);
            return (null, null, DateTime.MinValue);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get token for provider: {Provider}", provider);
            throw;
        }
    }

    public async Task RemoveTokenAsync(string provider)
    {
        try
        {
            _logger.LogInformation("Removing token for provider: {Provider}", provider);
            var response = await _httpClient.DeleteAsync($"api/auth/remove-token/{provider}");
            response.EnsureSuccessStatusCode();
            _logger.LogInformation("Token successfully removed for provider: {Provider}", provider);
            
            // Update cache
            _cachedTokens.Remove(provider);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to remove token for provider: {Provider}", provider);
            throw;
        }
    }

    public Dictionary<string, TokenData> GetAllTokens()
    {
        _logger.LogInformation("Getting all tokens from cache. Count: {Count}", _cachedTokens.Count);
        return new Dictionary<string, TokenData>(_cachedTokens);
    }
}