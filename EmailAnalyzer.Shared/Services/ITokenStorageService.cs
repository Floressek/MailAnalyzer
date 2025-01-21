using EmailAnalyzer.Shared.Models.Auth;

namespace EmailAnalyzer.Shared.Services;

/// <summary>
/// This interface represents the token storage service.
/// </summary>
public interface ITokenStorageService
{
    Task StoreTokenAsync(string provider, string accessToken, string refreshToken, DateTime expiresAt);
    Task<(string? accessToken, string? refreshToken, DateTime expiresAt)> GetTokenAsync(string provider);
    Task RemoveTokenAsync(string provider);
    Dictionary<string, TokenData> GetAllTokens();

}