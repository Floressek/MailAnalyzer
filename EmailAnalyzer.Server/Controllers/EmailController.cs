using System.Data;
using EmailAnalyzer.Server.Services.Database;
using EmailAnalyzer.Server.Services.Email;
using EmailAnalyzer.Server.Services.OpenAI;
using EmailAnalyzer.Shared.Models;
using EmailAnalyzer.Shared.Models.Auth;
using EmailAnalyzer.Shared.Models.Database;
using EmailAnalyzer.Shared.Services;
using EmailAnalyzer.Shared.Models.Email;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;

namespace EmailAnalyzer.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class EmailController : ControllerBase
{
    private readonly IEmailServiceFactory _emailServiceFactory;
    private readonly ITokenStorageService _tokenStorageService;
    private readonly ILogger<EmailController> _logger;
    private readonly OpenAIService _openAIService;
    private readonly MongoDBService _mongoDBService;
    private readonly EmailProcessingService _emailProcessingService;

    public EmailController(
        IEmailServiceFactory emailServiceFactory,
        ITokenStorageService tokenStorageService,
        ILogger<EmailController> logger,
        OpenAIService openAiService,
        MongoDBService mongoDbService,
        EmailProcessingService emailProcessingService)
    {
        _emailServiceFactory = emailServiceFactory;
        _tokenStorageService = tokenStorageService;
        _logger = logger;
        _openAIService = openAiService;
        _mongoDBService = mongoDbService;
        _emailProcessingService = emailProcessingService;
    }

    /// <summary>
    /// This module is used to test the connection to the specified provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    [HttpGet("test/{provider}")]
    public async Task<IActionResult> TestConnection(string provider)
    {
        var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
        if (accessToken == null)
        {
            return BadRequest(new { error = "No token found. Please authenticate first" });
        }

        return Ok(new
        {
            message = "Token found",
            expiresAt,
            hasRefreshToken = !string.IsNullOrEmpty(refreshToken) // check if refresh token is available
        });
    }

    /// <summary>
    /// Get emails from the specified provider between the specified dates.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <returns></returns>
    [HttpPost("{provider}")]
    public async Task<ActionResult<List<EmailMessage>>> GetEmails(
        string provider,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)

    {
        try
        {
            var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
            if (accessToken == null)
            {
                return Unauthorized("No token found. Please authenticate first");
            }

            // Jeśli token wygasa za mniej niż 5 minut, spróbuj go odświeżyć
            if (expiresAt <= DateTime.UtcNow.AddMinutes(5))
            {
                var emailService = _emailServiceFactory.GetService(provider);
                var refreshSuccess = await emailService.RefreshTokenAsync(new UserCredentials
                {
                    RefreshToken = refreshToken,
                    Provider = provider
                });

                if (!refreshSuccess)
                {
                    return Unauthorized("Token expired and refresh failed. Please re-authenticate");
                }

                // Pobierz nowy token po odświeżeniu FIXME: jak bysmy implementowali token resfresh to trzeba na klase przerobic TokenData!
                (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
            }

            var service = _emailServiceFactory.GetService(provider);
            var emails = await service.GetEmailsByDateAsync(startDate, endDate);
            return Ok(emails);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting emails for {Provider}", provider);
            return StatusCode(500, "Error fetching emails");
        }
    }

    /// <summary>
    /// This module is used to get the available date range for the specified provider.
    /// </summary>
    /// <param name="provider"></param>
    /// <returns></returns>
    [HttpGet("available-range/{provider}")]
    public async Task<ActionResult<DateRangeInfo>> GetAvailableDateRange(string provider)
    {
        try
        {
            var (accessToken, _, expiresAt) =
                await _tokenStorageService.GetTokenAsync(provider); // _ is used to discard the refresh token
            if (accessToken == null)
            {
                return Unauthorized("No token found. Please authenticate first");
            }

            if (expiresAt <= DateTime.UtcNow)
            {
                return Unauthorized("Token expired. Please re-authenticate");
            }

            return Ok(new DateRangeInfo
            {
                EarliestDate = DateTime.Today.AddMonths(-6), // FIXEME: CONSTRAINTS FOR THE AVAIABLE DATES
                LatestDate = DateTime.Today,
                DefaultStartDate = DateTime.Today.AddMonths(-1), // FIXEME: CONSTRAINTS FOR THE AVAIABLE DATES
                DefaultEndDate = DateTime.Today,
                Provider = provider
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting date range for {Provider}", provider);
            return StatusCode(500, "Error fetching date range");
        }
    }

    [HttpPost("{provider}/analyze")]
    public async Task<ActionResult<EmailSummaryResult>> AnalyzeEmails(
        string provider,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            // Get emails
            var emails = await GetEmailsInternalAsync(provider, startDate, endDate);
            if (!emails.Any())
            {
                return BadRequest("No emails found for the specified date range");
            }

            // Generate summary
            var summary = await _openAIService.SummarizeEmailAsync(emails, provider);

            // Mapping to EmailSummaryResult from EmailSummaryDocument czyli bierzemy tylko te pola ktore sa potrzebne
            var summaryDocument = new EmailSummaryDocument
            {
                Provider = summary.Provider, // provider
                DateRange = new DateRange // StartDate, EndDate
                {
                    Start = summary.StartDate,
                    End = summary.EndDate
                },
                TotalEmails = summary.TotalEmails,
                EmailIds = summary.BatchSummaries.SelectMany(b => b.EmailIds).ToList(), // EmailIds
                Summary = summary.FinalSummary, // FinalSummary
                TopicClusters = summary.BatchSummaries.Select(b => new TopicCluster // tutaj mamy Topic, Count, Keywords
                {
                    Topic = b.Summary,
                    Count = b.EmailCount,
                    Keywords = new List<string>() // możesz dodać ekstrakcję słów kluczowych
                }).ToList(),
                Embedding = summary.BatchSummaries.FirstOrDefault()?.Embedding ??
                            new List<float>(), // Embedding do dodania
                CreatedAt = DateTime.UtcNow
            };

            // Save to MongoDB
            await _mongoDBService.SaveSummaryAsync(summaryDocument);

            return Ok(summary);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing emails");
            return StatusCode(500, "Error analyzing emails");
        }
    }

    [HttpGet("{provider}/summaries")]
    public async Task<ActionResult<List<EmailSummaryResult>>> GetSummaries(
        string provider,
        [FromQuery] DateTime startDate,
        [FromQuery] DateTime endDate)
    {
        try
        {
            var summaries = await _mongoDBService.GetSummariesAsync(provider, startDate, endDate);
            return Ok(summaries);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting summaries");
            return StatusCode(500, "Error fetching summaries");
        }
    }

    // Helper method for GetEmails endpoint
    private async Task<List<EmailMessage>> GetEmailsInternalAsync(
        string provider,
        DateTime startDate,
        DateTime endDate)
    {
        var (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
        if (accessToken == null)
        {
            throw new UnauthorizedAccessException("No token found. Please authenticate first");
        }

        // Refresh token if needed
        if (expiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var emailService = _emailServiceFactory.GetService(provider);
            var refreshSuccess = await emailService.RefreshTokenAsync(new UserCredentials
            {
                RefreshToken = refreshToken,
                Provider = provider
            });

            if (!refreshSuccess)
            {
                throw new UnauthorizedAccessException("Token expired and refresh failed. Please re-authenticate");
            }

            (accessToken, refreshToken, expiresAt) = await _tokenStorageService.GetTokenAsync(provider);
        }

        var service = _emailServiceFactory.GetService(provider);
        var emails = await service.GetEmailsByDateAsync(startDate, endDate);

        // Zapisujemy kazdy mail do mongosa ;))
        // Przetwarzaj maile w tle - generuj embeddingi i zapisuj do MongoDB
        _ = Task.Run(async () =>
        {
            try
            {
                await _emailProcessingService.ProcessEmailBatchAsync(emails, provider);
                _logger.LogInformation("Successfully processed and saved {Count} emails with embeddings", emails.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process emails batch");
            }
        });

        return emails;
    }

    /// <summary>
    /// Heltcheck for the MongoDB connection.
    /// </summary>
    /// <returns></returns>
    [HttpGet("mongo")]
    public async Task<IActionResult> CheckMongo()
    {
        try
        {
            var collections = await _mongoDBService.ListCollectionsAsync();
            return Ok(new
            {
                status = "healthy",
                collections = collections,
                timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                status = "unhealthy",
                error = ex.Message,
                timestamp = DateTime.UtcNow
            });
        }
    }

    /// <summary>
    /// This module is used to search for emails based on the provided query.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="query"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    [HttpGet("{provider}/search")]
    public async Task<ActionResult<SearchResult>> SemanticSearch(
        string provider,
        [FromQuery] string query,
        [FromQuery] DateTime? startDate = null,
        [FromQuery] DateTime? endDate = null,
        [FromQuery] int limit = 5)
    {
        try
        {
            _logger.LogInformation(
                "Semantic search request. Query: {Query}, Provider: {Provider}",
                query, provider);

            var queryEmbedding = await _openAIService.GenerateEmbeddingAsync(query);
            var similarEmails = await _mongoDBService.FindSimilarEmailsAsync(
                queryEmbedding,
                provider,
                startDate,
                endDate,
                limit);

            // Dodajemy więcej kontekstu do odpowiedzi
            var result = new SearchResult
            {
                Query = query,
                TotalResults = similarEmails.Count,
                Results = similarEmails.Select(email => new SearchResultItem
                {
                    Subject = email.Subject,
                    From = email.From,
                    ReceivedDate = email.ReceivedDate,
                    Similarity = email.Similarity,
                    Content = email.Content
                }).ToList()
            };

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during semantic search");
            return StatusCode(500, "Error performing semantic search");
        }
    }

// Nowe klasy do zwracania wyników:
    public class SearchResult
    {
        public string Query { get; set; } = string.Empty;
        public int TotalResults { get; set; }
        public List<SearchResultItem> Results { get; set; } = new();
    }

    public class SearchResultItem
    {
        public string Subject { get; set; } = string.Empty;
        public string From { get; set; } = string.Empty;
        public DateTime ReceivedDate { get; set; }
        public double Similarity { get; set; }
        public string Content { get; set; } = string.Empty;
    }
}