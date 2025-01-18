namespace EmailAnalyzer.Shared.Models.Auth;

/// <summary>
/// This class represents the user credentials for the authentication service.
/// </summary>
public class UserCredentials
{
    public string UserId { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime TokenExpiry { get; set; }
}