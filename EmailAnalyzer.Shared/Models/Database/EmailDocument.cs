using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace EmailAnalyzer.Shared.Models.Database;

public class EmailDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
    public string EmailId { get; set; } = string.Empty; // ID z Gmail/Outlook
    public string Subject { get; set; } = string.Empty;
    public string From { get; set; } = string.Empty;
    public DateTime ReceivedDate { get; set; }
    public string Content { get; set; } = string.Empty;
    public List<float> Embedding { get; set; } = new();
    public DateTime FetchedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AnalyzedAt { get; set; }
    public List<string> Labels { get; set; } = new();
    
    [BsonIgnoreIfNull]
    public double Similarity { get; set; }
}

public class SentimentInfo
{
    public float Score { get; set; }
    public string Label { get; set; } = string.Empty;
}

public class EmailSummaryDocument
{
    [BsonId] // Primary key do kolekcji
    [BsonRepresentation(BsonType.ObjectId)] // MongoDB object ID uzywany do reprezentacji stringow
    public string Id { get; set; } = string.Empty;

    public string Provider { get; set; } = string.Empty;
    public DateRange DateRange { get; set; } = new();
    public List<string> EmailIds { get; set; } = new();
    public int TotalEmails { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<TopicCluster> TopicClusters { get; set; } = new();
    public List<float> Embedding { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DateRange
{
    public DateTime Start { get; set; }
    public DateTime End { get; set; }
}

public class TopicCluster
{
    public string Topic { get; set; } = string.Empty;
    public int Count { get; set; }
    public List<string> Keywords { get; set; } = new();
}