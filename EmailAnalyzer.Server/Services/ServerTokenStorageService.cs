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

                if (tokens != null)
                {
                    foreach (var (key, value) in tokens)
                    {
                        _tokens.TryAdd(key, value);
                    }
                    _logger.LogInformation("Tokens successfully loaded from file. Token count: {Count}", tokens.Count);
                }
                else
                {
                    _logger.LogWarning("No tokens found in file.");
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

        if (string.IsNullOrEmpty(accessToken))
        {
            _logger.LogError("AccessToken is null or empty for provider: {Provider}", provider);
        }

        _tokens.AddOrUpdate(provider,
            (accessToken, refreshToken, expiresAt),
            (_, _) => (accessToken, refreshToken, expiresAt));

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storageFilePath)!);

            // Konwertuj ConcurrentDictionary na Dictionary
            var serializableTokens = _tokens.ToDictionary(entry => entry.Key, entry => entry.Value);
            var json = JsonSerializer.Serialize(serializableTokens);

            _logger.LogInformation("Serialized token JSON: {Json}", json);
            File.WriteAllText(_storageFilePath, json);

            _logger.LogInformation("Token successfully saved to file at {Path}", _storageFilePath);
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
        _logger.LogInformation("Fetching all tokens. Current tokens count: {Count}", _tokens.Count);

        foreach (var (key, value) in _tokens)
        {
            _logger.LogDebug("Token for provider {Provider}: AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
                key, value.accessToken, value.refreshToken, value.expiresAt);
        }

        return _tokens.ToDictionary(entry => entry.Key, entry => entry.Value);
    }
}
