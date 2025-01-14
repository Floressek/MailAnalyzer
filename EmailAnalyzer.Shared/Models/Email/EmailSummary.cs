// W EmailAnalyzer.Shared/Models/Email/EmailSummary.cs
namespace EmailAnalyzer.Shared.Models.Email;

/// <summary>
/// This class represents the summary of an email.
/// </summary>
public class EmailSummary
{
    public string Id { get; set; } = string.Empty;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public string Summary { get; set; } = string.Empty;
    public List<string> KeyTopics { get; set; } = new();
    public List<EmailMessage> RelatedEmails { get; set; } = new();
}