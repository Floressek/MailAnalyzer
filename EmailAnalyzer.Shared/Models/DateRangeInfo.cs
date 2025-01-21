namespace EmailAnalyzer.Shared.Models;

/// <summary>
/// This class contains information about the date range for the emails.
/// </summary>
public class DateRangeInfo
{
    public DateTime EarliestDate { get; set; }
    public DateTime LatestDate { get; set; }
    public DateTime DefaultStartDate { get; set; }
    public DateTime DefaultEndDate { get; set; }
    public string Provider { get; set; } = string.Empty;
}

/// <summary>
/// This class represents the request to analyze emails.
/// </summary>
public class EmailAnalysisRequest
{
    public string Provider { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}