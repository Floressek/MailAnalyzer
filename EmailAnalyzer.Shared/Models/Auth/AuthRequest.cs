namespace EmailAnalyzer.Shared.Models.Auth;

/// <summary>
/// This class represents the request to authenticate a user
/// </summary>
public class AuthRequest
{
    public string Provider { get; set; } = string.Empty;
    public string AuthCode { get; set; } = string.Empty;
}