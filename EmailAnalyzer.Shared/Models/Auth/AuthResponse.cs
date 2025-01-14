namespace EmailAnalyzer.Shared.Models.Auth;
/// <summary>
/// This class represents the response from the authentication service.
/// </summary>
public class AuthResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? Error { get; set; }
    public DateTime ExpiresAt { get; set; }
}