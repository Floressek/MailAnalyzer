using EmailAnalyzer.Shared.Models.Email;

namespace EmailAnalyzer.Shared.DTOs;

/// <summary>
/// This class represents the response from the email analysis service.
/// </summary>
public class EmailAnalysisResponse
{
    public List<EmailMessage> Emails { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public DateTime AnalysisDate { get; set; } = DateTime.UtcNow;
}