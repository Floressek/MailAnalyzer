namespace EmailAnalyzer.Shared.DTOs;

/// <summary>
/// This class represents the request to the email analysis service.
/// </summary>
public class EmailAnalysisRequest
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}