using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EmailAnalyzer.Shared.Models.Email;
/// <summary>
/// This class represents the result of summarizing a set of emails.
/// </summary>
public class EmailSummaryResult
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;
    
    public string Provider { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public int TotalEmails { get; set; }
    public List<BatchSummary> BatchSummaries { get; set; } = new();
    public string FinalSummary { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// This class represents a summary of a batch of emails.
/// </summary>
public class BatchSummary
{
    public int BatchNumber { get; set; }
    public int EmailCount { get; set; }
    public List<string> EmailIds { get; set; } = new();
    public string Summary { get; set; } = string.Empty;
    public List<float> Embedding { get; set; } = new();
}
