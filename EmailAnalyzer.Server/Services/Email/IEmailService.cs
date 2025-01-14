using EmailAnalyzer.Shared.Models.Email;
using EmailAnalyzer.Shared.Models.Auth;

namespace EmailAnalyzer.Server.Services.Email;

public interface IEmailService
{
    Task <string> GetAutorizationUrlAsync();
    Task<AuthResponse> AuthenticateAsync(string authCode);
    Task<List<EmailMessage>> GetEmailsByDateAsync(DateTime startDate, DateTime endDate);
    Task<bool> RefreshTokenAsync(UserCredentials userCredentials);
}