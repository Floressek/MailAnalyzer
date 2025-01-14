using EmailAnalyzer.Shared.Services;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace EmailAnalyzer.Server.Services;

public class ServerTokenStorageService : ITokenStorageService
{
    private readonly ILogger<ServerTokenStorageService> _logger;
    private static readonly ConcurrentDictionary<string, (string accessToken, string refreshToken, DateTime expiresAt)> _tokens = new();

    public ServerTokenStorageService(ILogger<ServerTokenStorageService> logger)
    {
        _logger = logger;
    }

    public Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt)
    {
        _tokens.AddOrUpdate(provider, 
            (accessToken, refreshToken, expiresAt), 
            (_, _) => (accessToken, refreshToken, expiresAt));
        
        _logger.LogInformation("Token stored for {Provider}", provider);
        return Task.CompletedTask;
    }

    public Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider)
    {
        if (_tokens.TryGetValue(provider, out var token))
        {
            return Task.FromResult<(string?, string?, DateTime)>((token.accessToken, token.refreshToken, token.expiresAt));
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
}