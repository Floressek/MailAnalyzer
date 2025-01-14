namespace EmailAnalyzer.Shared.Models.Email;

/// <summary>
/// This class represents an email message.
/// </summary>
public class EmailMessage
{
    public string Id { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string Preview { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty; // "outlook" lub "gmail"
    public string VectorId { get; set; } = string.Empty; // ID w bazie wektorowej
}