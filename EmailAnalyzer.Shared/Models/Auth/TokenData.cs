namespace EmailAnalyzer.Shared.Models.Auth;

public class TokenData
{
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime ExpiresAt { get; set; }
}