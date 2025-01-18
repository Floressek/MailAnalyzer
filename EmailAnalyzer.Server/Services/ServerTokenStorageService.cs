using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using EmailAnalyzer.Shared.Models.Auth;

namespace EmailAnalyzer.Server.Services;

public class ServerTokenStorageService : ITokenStorageService
{
    private readonly ILogger<ServerTokenStorageService> _logger;

    private static readonly ConcurrentDictionary<string, (string accessToken, string refreshToken, DateTime expiresAt)>
        _tokens = new();

    private readonly string _storageFilePath = "/token_vault/tokens.json";

    public ServerTokenStorageService(ILogger<ServerTokenStorageService> logger)
    {
        _logger = logger;
        LoadTokens();
    }

    private void LoadTokens()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                var json = File.ReadAllText(_storageFilePath);
                var tokens = JsonSerializer.Deserialize<Dictionary<string, (string, string, DateTime)>>(json);
                foreach (var (key, value) in tokens!)
                {
                    _tokens.TryAdd(key, value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load tokens from file");
        }
    }

    public Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt)
    {
        _logger.LogInformation("Storing token for provider: {Provider}", provider);
        _logger.LogDebug("Token details: AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
            accessToken, refreshToken, expiresAt);

        _tokens.AddOrUpdate(provider,
            (accessToken, refreshToken, expiresAt),
            (_, _) => (accessToken, refreshToken, expiresAt));

        try
        {
            // Upewnij się że katalog istnieje
            Directory.CreateDirectory(Path.GetDirectoryName(_storageFilePath)!);

            // Zapisz do pliku
            var json = JsonSerializer.Serialize(_tokens);
            File.WriteAllText(_storageFilePath, json);

            _logger.LogInformation("Token stored for {Provider} and saved to file at {Path}", provider,
                _storageFilePath);
            _logger.LogDebug(
                "Stored token details: AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
                accessToken, refreshToken, expiresAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens to file");
        }

        return Task.CompletedTask;
    }

    public Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider)
    {
        if (_tokens.TryGetValue(provider, out var token))
        {
            _logger.LogInformation("Retrieved token for {Provider}", provider);
            return Task.FromResult<(string?, string?, DateTime)>((token.accessToken, token.refreshToken,
                token.expiresAt));
        }

        _logger.LogWarning("Token not found for {Provider}", provider);
        return Task.FromResult<(string?, string?, DateTime)>((null, null, DateTime.MinValue));
    }

    public Task RemoveTokenAsync(string provider)
    {
        _tokens.TryRemove(provider, out _);
        _logger.LogInformation("Tokens removed for {Provider}", provider);
        return Task.CompletedTask;
    }

    public Dictionary<string, (string accessToken, string refreshToken, DateTime expiresAt)> GetAllTokens()
    {
        return _tokens.ToDictionary(entry => entry.Key, entry => entry.Value);
    }
}
