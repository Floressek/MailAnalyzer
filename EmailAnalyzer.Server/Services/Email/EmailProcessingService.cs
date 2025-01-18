using EmailAnalyzer.Server.Services.Database;
using EmailAnalyzer.Server.Services.OpenAI;
using EmailAnalyzer.Shared.Models.Database;
using EmailAnalyzer.Shared.Models.Email;
using Microsoft.AspNetCore.Mvc;

namespace EmailAnalyzer.Server.Services.Email;

public class EmailProcessingService
{
    private readonly OpenAIService _openAIService;
    private readonly MongoDBService _mongoDBService;
    private readonly ILogger<EmailProcessingService> _logger;
    private const int BATCH_SIZE = 10; // Mniejszy batch size dla embeddings

    public EmailProcessingService(OpenAIService openAIService,
        MongoDBService mongoDBService,
        ILogger<EmailProcessingService> logger)
    {
        _openAIService = openAIService;
        _mongoDBService = mongoDBService;
        _logger = logger;
    }

    public async Task ProcessEmailBatchAsync(List<EmailMessage> emails, string provider)
    {
        // Divide emails into batches
        var batches = emails.Chunk(BATCH_SIZE);
        foreach (var batch in batches)
        {
            await ProcessEmailsAsync(batch.ToList(), provider);
        }
    }

    /// <summary>
    /// This method processes a batch of emails, generates embeddings and saves them to the database.
    /// </summary>
    /// <param name="emails"></param>
    /// <param name="provider"></param>
    private async Task ProcessEmailsAsync(List<EmailMessage> emails, string provider)
    {
        foreach (var email in emails)
        {
            try
            {
                // Przygotuj treść do embeddingu
                var emailContent =
                    $"Subject: {email.Subject}\nFrom: {email.From}\nDate: {email.ReceivedDate}\nContent: {email.Preview}";

                // Generuj embedding
                var embedding = await _openAIService.GenerateEmbeddingAsync(emailContent);

                // Zapisz embedding do bazy danych
                var emailDoc = new EmailDocument
                {
                    Provider = provider,
                    EmailId = email.Id,
                    Subject = email.Subject,
                    From = email.From,
                    Content = email.Preview,
                    ReceivedDate = email.ReceivedDate,
                    FetchedAt = DateTime.UtcNow,
                    Embedding = embedding,
                };

                // Zapisz do MongoDB
                await _mongoDBService.SaveEmailAsync(emailDoc);
                _logger.LogInformation("Processed and saved email {EmailId} with embedding", email.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process email {EmailId}", email.Id);
            }
        }
    }
    
    /// <summary>
    /// This method retrieves emails from the database based on the specified criteria.
    /// </summary>
    /// <param name="query"></param>
    /// <param name="provider"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public async Task<List<EmailDocument>> FindSimilarEmailsAsync(
        string query, 
        string provider, 
        DateTime? startDate = null, 
        DateTime? endDate = null,
        int limit = 5)
    {
        var queryEmbedding = await _openAIService.GenerateEmbeddingAsync(query); // Generate embedding for query
        return await _mongoDBService.FindSimilarEmailsAsync(queryEmbedding, provider, startDate, endDate, limit); // Find similar emails
    }
}