using System.Text;
using System.Text.Json;
using EmailAnalyzer.Shared.Models.Email;
using Microsoft.Extensions.Options;

namespace EmailAnalyzer.Server.Services.OpenAI;

public class OpenAIConfiguration
{
    public string ApiKey { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = "text-embedding-ada-002";
    public string CompletionModel { get; set; } = "gpt-4o-2024-08-06";
}

public class OpenAIService
{
    private readonly HttpClient _httpClient;
    private readonly OpenAIConfiguration _config;
    private readonly ILogger<OpenAIService> _logger;
    private const int BATCH_SIZE = 20;

    public OpenAIService(HttpClient httpClient,
        IOptions<OpenAIConfiguration> config,
        ILogger<OpenAIService> logger)
    {
        _httpClient = httpClient;
        _config = config.Value;
        _logger = logger;

        _httpClient.BaseAddress = new Uri("https://api.openai.com/v1/");
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_config.ApiKey}");
    }

    public async Task<EmailSummaryResult> SummarizeEmailAsync(List<EmailMessage> emails, string provider)
    {
        var result = new EmailSummaryResult
        {
            Provider = provider,
            StartDate = emails.Min(e => e.ReceivedDate),
            EndDate = emails.Max(e => e.ReceivedDate),
            TotalEmails = emails.Count
        };

        // Divide emails into batches
        var batches = emails
            .Select((email, index) => new { Email = email, Index = index })
            .GroupBy(x => x.Index / BATCH_SIZE)
            .Select(g => g.Select(x => x.Email).ToList()) // Convert to list of emails
            .ToList();

        _logger.LogInformation("Divided emails into {BatchCount} batches", batches.Count);

        // Process each batch
        for (var i = 0; i < batches.Count; i++)
        {
            var batch = batches[i];
            var batchSummary = await ProcessBatchAsync(batch, i);
            result.BatchSummaries.Add(batchSummary);
        }

        // Generate final summary if multiple batches
        if (batches.Count > 1)
        {
            var batchSummaries = result.BatchSummaries.Select(b => b.Summary).ToList(); // Get summaries
            result.FinalSummary = await GenerateFinalSummaryAsync(batchSummaries);
        }
        else
        {
            result.FinalSummary = result.BatchSummaries.First().Summary;
        }

        return result;
    }

    private async Task<BatchSummary> ProcessBatchAsync(List<EmailMessage> emails, int batchNumber)
    {
        var batchContent = new StringBuilder();
        foreach (var email in emails)
        {
            batchContent.AppendLine($"Subject: {email.Subject}");
            batchContent.AppendLine($"From: {email.From}");
            batchContent.AppendLine($"Date: {email.ReceivedDate:yyyy-MM-dd HH:mm}");
            batchContent.AppendLine($"Preview: {email.Preview}");
            batchContent.AppendLine();
        }

        var summary = await GenerateSummaryAsync(batchContent.ToString());
        var embedding = await GenerateEmbeddingAsync(summary);

        return new BatchSummary
        {
            BatchNumber = batchNumber,
            EmailCount = emails.Count,
            EmailIds = emails.Select(e => e.Id).ToList(),
            Summary = summary,
            Embedding = embedding
        };
    }

    private async Task<string> GenerateSummaryAsync(string content)
    {
        var request = new
        {
            model = _config.CompletionModel,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content =
                        "You are an expert at analyzing and summarizing email content. " +
                        "Provide a concise but comprehensive summary of the key points, patterns, and important " +
                        "information from the provided emails."
                },
                new
                {
                    role = "user",
                    content = $"Please analyze and summarize the following email batch:\n\n{content}"
                }
            },
            temperature = 0.3,
            max_tokens = 20000
        };

        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        response.EnsureSuccessStatusCode();

        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ??
               string.Empty;
    }
    
    private async Task<List<float>> GenerateEmbeddingAsync(string text)
    {
        var request = new
        {
            model = _config.EmbeddingModel,
            input = text
        };

        // Send request to OpenAI API
        var response = await _httpClient.PostAsJsonAsync("embeddings", request);
        response.EnsureSuccessStatusCode();
        
        // Parse response
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        var embedding = result.GetProperty("data")[0].GetProperty("embedding");
        
        // Deserialize embedding
        return JsonSerializer.Deserialize<List<float>>(embedding) ?? new List<float>();
    }
    
    private async Task<string> GenerateFinalSummaryAsync(List<string> batchSummaries)
    {
        var summariesContent = string.Join("\n\n", batchSummaries);

        var request = new
        {
            model = _config.CompletionModel,
            messages = new[]
            {
                new
                {
                    role = "system",
                    content = "You are an expert at analyzing and combining multiple email summaries into a cohesive final summary. Focus on identifying overall patterns, trends, and key insights."
                },
                new
                {
                    role = "user",
                    content = $"Please create a final summary combining these batch summaries:\n\n{summariesContent}"
                }
            },
            temperature = 0.3,
            max_tokens = 25000
        };
        
        // Send request to OpenAI API
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request);
        response.EnsureSuccessStatusCode();
        
        // Parse response
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return result.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? "";
    }
}