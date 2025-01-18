using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Text.Json;
using EmailAnalyzer.Shared.Models.Auth;

namespace EmailAnalyzer.Server.Services;

public class ServerTokenStorageService : ITokenStorageService
{
    private readonly ILogger<ServerTokenStorageService> _logger;

    private static readonly ConcurrentDictionary<string, TokenData> _tokens = new();

    private readonly string _storageFilePath = "/token_vault/tokens.json";

    public ServerTokenStorageService(ILogger<ServerTokenStorageService> logger)
    {
        _logger = logger;
        EnsureStoragePathExists();
        LoadTokens();
    }
    
    private void EnsureStoragePathExists()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storageFilePath)!);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure storage path exists at {Path}", _storageFilePath);
        }
    }

    private void LoadTokens()
    {
        try
        {
            if (File.Exists(_storageFilePath))
            {
                var json = File.ReadAllText(_storageFilePath);
                _logger.LogInformation("Loaded JSON from file: {Json}", json);

                var tokens = JsonSerializer.Deserialize<Dictionary<string, TokenData>>(json);

                if (tokens != null)
                {
                    foreach (var (key, value) in tokens)
                    {
                        _tokens.TryAdd(key, value);
                        _logger.LogInformation(
                            "Loaded token for provider: {Provider}, AccessToken: {AccessToken}, RefreshToken: {RefreshToken}, ExpiresAt: {ExpiresAt}",
                            key, value.AccessToken, value.RefreshToken, value.ExpiresAt);
                    }

                    _logger.LogInformation("Tokens successfully loaded. Count: {Count}", tokens.Count);
                }
                else
                {
                    _logger.LogWarning("Deserialized tokens are null.");
                }
            }
            else
            {
                _logger.LogWarning("Token file does not exist at path: {Path}", _storageFilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading tokens from file");
        }
    }



    public async Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt)
    {
        _tokens.AddOrUpdate(provider,
            new TokenData { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresAt = expiresAt },
            (_, _) => new TokenData { AccessToken = accessToken, RefreshToken = refreshToken, ExpiresAt = expiresAt });

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_storageFilePath)!);
            var serializableTokens = _tokens.ToDictionary(entry => entry.Key, entry => entry.Value);
            var json = JsonSerializer.Serialize(serializableTokens, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            await File.WriteAllTextAsync(_storageFilePath, json);
            _logger.LogInformation("Token successfully saved to file at {Path}", _storageFilePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save tokens to file");
        }
    }




    public Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider)
    {
        if (_tokens.TryGetValue(provider, out var token))
        {
            _logger.LogInformation(
                "Retrieved token for {Provider}: AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
                provider, token.AccessToken, token.RefreshToken, token.ExpiresAt);

            return Task.FromResult<(string?, string?, DateTime)>((token.AccessToken, token.RefreshToken, token.ExpiresAt));
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

    public Dictionary<string, TokenData> GetAllTokens()
    {
        _logger.LogInformation("Fetching all tokens. Current tokens count: {Count}", _tokens.Count);

        foreach (var (key, value) in _tokens)
        {
            _logger.LogDebug("Token in _tokens: Provider={Provider}, AccessToken={AccessToken}, RefreshToken={RefreshToken}, ExpiresAt={ExpiresAt}",
                key, value.AccessToken, value.RefreshToken, value.ExpiresAt);
        }

        return _tokens.ToDictionary(entry => entry.Key, entry => entry.Value);
    }

}