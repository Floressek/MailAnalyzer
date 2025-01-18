using EmailAnalyzer.Shared.Models.Database;
using MongoDB.Driver;
using EmailAnalyzer.Shared.Models.Email;
using Microsoft.Extensions.Options;
using MongoDB.Bson;


namespace EmailAnalyzer.Server.Services.Database;

/// <summary>
/// This class provides MongoDB database access for the application.
/// </summary>
public class MongoDBConfiguration
{
    public string Host { get; set; } = string.Empty;
    public string Port { get; set; } = string.Empty;
    public string User { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "emailanalyzer";

    public string ConnectionString =>
        $"mongodb://{User}:{Password}@{Host}:{Port}";
}

/// <summary>
/// This class provides MongoDB database access for the application.
/// </summary>
public class MongoDBService
{
    private readonly IMongoDatabase _database;
    private readonly IMongoCollection<EmailDocument> _emails;
    private readonly IMongoCollection<EmailSummaryDocument> _summaries;
    private readonly ILogger<MongoDBService> _logger;

    public MongoDBService(
        IOptions<MongoDBConfiguration> config,
        ILogger<MongoDBService> logger)
    {
        _logger = logger;

        try
        {
            var mongoConnectionUrl = config.Value.ConnectionString;
            _logger.LogInformation("Initializing MongoDB with connection string: {ConnectionString}",
                mongoConnectionUrl.Replace(config.Value.Password, "****"));

            var client = new MongoClient(mongoConnectionUrl);
            _database = client.GetDatabase(config.Value.DatabaseName);

            _emails = _database.GetCollection<EmailDocument>("emails");
            _summaries = _database.GetCollection<EmailSummaryDocument>("summaries");

            CreateIndexes();
            _logger.LogInformation("MongoDB service initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize MongoDB service");
            throw;
        }
    }

    /// <summary>
    /// This method creates indexes for the MongoDB collections.
    /// </summary>
    private void CreateIndexes()
    {
        // Define indexes for the emails collection
        var emailsIndexes = new[]
        {
            // Create index for provider and email ID
            new CreateIndexModel<EmailDocument>(
                Builders<EmailDocument>.IndexKeys
                    .Ascending(e => e.Provider)
                    .Ascending(e => e.EmailId),
                new CreateIndexOptions { Unique = true }
            ),
            // Create index for provider and received date
            new CreateIndexModel<EmailDocument>(
                Builders<EmailDocument>.IndexKeys
                    .Ascending(e => e.Provider)
                    .Descending(e => e.ReceivedDate)
            ),
            // Create index for FetchedAt date
            new CreateIndexModel<EmailDocument>(
                Builders<EmailDocument>.IndexKeys
                    .Ascending(e => e.FetchedAt),
                new CreateIndexOptions
                {
                    ExpireAfter = TimeSpan.FromDays(30) // TTL index - usuwa dokumenty po 30 dniach
                }
            )
        };

        // Summarize indexes for the summaries collection
        var summariesIndexes = new[]
        {
            // Create index for provider, start date, and end date
            new CreateIndexModel<EmailSummaryDocument>(
                Builders<EmailSummaryDocument>.IndexKeys
                    .Ascending(s => s.Provider)
                    .Ascending("DateRange.Start")
                    .Ascending("DateRange.End")
            )
        };

        // Create indexes
        _emails.Indexes.CreateMany(emailsIndexes);
        _summaries.Indexes.CreateMany(summariesIndexes);
    }

    /// <summary>
    /// This method saves an email document to the database.
    /// </summary>
    /// <param name="email"></param>
    /// <returns></returns>
    public async Task<EmailDocument> SaveEmailAsync(EmailDocument email)
    {
        try
        {
            // Save email to database And = Logical AND, Eq = Equal, Gte = Greater than or equal, Lte = Less than or equal
            var filter = Builders<EmailDocument>.Filter.And(
                Builders<EmailDocument>.Filter.Eq(e => e.Provider, email.Provider),
                Builders<EmailDocument>.Filter.Eq(e => e.EmailId, email.EmailId)
            );

            // Update document if it already exists, otherwise insert new document
            var update = Builders<EmailDocument>.Update
                .Set(e => e.Subject, email.Subject)
                .Set(e => e.From, email.From)
                .Set(e => e.Content, email.Content)
                .Set(e => e.ReceivedDate, email.ReceivedDate)
                .Set(e => e.FetchedAt, DateTime.UtcNow)
                .Set(e => e.Embedding, email.Embedding); // Dodane embeddigi

            var options = new UpdateOptions { IsUpsert = true }; // Insert if not exists

            await _emails.UpdateOneAsync(filter, update, options);
            return email;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save email doc.");
            throw;
        }
    }

    /// <summary>
    /// This method retrieves emails from the database based on the specified criteria.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public async Task<List<EmailDocument>> GetEmailsAsync(
        string provider,
        DateTime startDate,
        DateTime endDate,
        int limit = 100)
    {
        try
        {
            var filter = Builders<EmailDocument>.Filter.And(
                Builders<EmailDocument>.Filter.Eq(e => e.Provider, provider), // Filtruj po dostawcy (provider)
                Builders<EmailDocument>.Filter.Gte(e => e.ReceivedDate,
                    startDate), // Filtruj po dacie odbioru (minimalna)
                Builders<EmailDocument>.Filter.Lte(e => e.ReceivedDate,
                    endDate) // Filtruj po dacie odbioru (maksymalna)
            );

            return await _emails
                .Find(filter)
                .Sort(Builders<EmailDocument>.Sort.Descending(e => e.ReceivedDate))
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve emails");
            throw;
        }
    }


    /// <summary>
    /// This method saves an email summary document to the database.
    /// </summary>
    /// <param name="summary"></param>
    /// <returns></returns>
    public async Task<EmailSummaryDocument> SaveSummaryAsync(EmailSummaryDocument summary)
    {
        try
        {
            await _summaries.InsertOneAsync(summary);
            return summary;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save summary");
            throw;
        }
    }

    /// <summary>
    /// This method retrieves summaries from the database based on the specified criteria.
    /// </summary>
    /// <param name="provider"></param>
    /// <param name="startDate"></param>
    /// <param name="endDate"></param>
    /// <param name="limit"></param>
    /// <returns></returns>
    public async Task<List<EmailSummaryDocument>> GetSummariesAsync(
        string provider,
        DateTime startDate,
        DateTime endDate,
        int limit = 10)
    {
        try
        {
            var filter = Builders<EmailSummaryDocument>.Filter.And(
                Builders<EmailSummaryDocument>.Filter.Eq(s => s.Provider, provider),
                Builders<EmailSummaryDocument>.Filter.Gte("DateRange.Start", startDate),
                Builders<EmailSummaryDocument>.Filter.Lte("DateRange.End", endDate)
            );

            return await _summaries
                .Find(filter)
                .Sort(Builders<EmailSummaryDocument>.Sort.Descending(s => s.CreatedAt))
                .Limit(limit)
                .ToListAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve summaries");
            throw;
        }
    }

    /// <summary>
    /// This method retrieves collections from the database.
    /// </summary>
    /// <returns></returns>
    public async Task<List<string>> ListCollectionsAsync()
    {
        try
        {
            var collections = new List<string>();
            var cursor = await _database.ListCollectionNamesAsync();

            while (await cursor.MoveNextAsync())
            {
                collections.AddRange(cursor.Current);
            }

            return collections;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list collections");
            throw;
        }
    }

    // public async Task<List<EmailDocument>> FindSimilarEmailsAsync(
    //     List<float> queryEmbedding,
    //     string provider,
    //     DateTime? startDate = null,
    //     DateTime? endDate = null,
    //     int limit = 5)
    // {
    //     try
    //     {
    //         var filterBuilder = Builders<EmailDocument>.Filter;
    //         var filters = new List<FilterDefinition<EmailDocument>>
    //         {
    //             filterBuilder.Eq(e => e.Provider, provider)
    //         };
    //
    //         if (startDate.HasValue)
    //             filters.Add(filterBuilder.Gte(e => e.ReceivedDate, startDate.Value));
    //         if (endDate.HasValue)
    //             filters.Add(filterBuilder.Lte(e => e.ReceivedDate, endDate.Value));
    //
    //         var filter = filterBuilder.And(filters);
    //
    //         // Find similar emails
    //         var vector = BsonValue.Create(queryEmbedding);
    //
    //         // Dodaj operację wyszukiwania wektorowego
    //         // Możemy użyć $vectorSearch jeśli mamy Atlas z obsługą wektorów
    //         var pipeline = new[]
    //         {
    //             // Filter out documents with null or missing embedding
    //             new BsonDocument("$match", new BsonDocument("embedding", new BsonDocument("$ne", BsonNull.Value))),
    //
    //             // Compute similarity
    //             new BsonDocument("$addFields", new BsonDocument
    //             {
    //                 {
    //                     "similarity", new BsonDocument("$function", new BsonDocument
    //                     {
    //                         {
    //                             "body",
    //                             "function(v1, v2) { return v1.reduce((acc, val, i) => acc + val * v2[i], 0) / (Math.sqrt(v1.reduce((acc, val) => acc + val * val, 0)) * Math.sqrt(v2.reduce((acc, val) => acc + val * val, 0))); }"
    //                         },
    //                         { "args", new BsonArray { "$embedding", new BsonArray(queryEmbedding) } },
    //                         { "lang", "js" }
    //                     })
    //                 }
    //             }),
    //
    //             // Sort by similarity
    //             new BsonDocument("$sort", new BsonDocument("similarity", -1)),
    //
    //             // Limit results
    //             new BsonDocument("$limit", limit)
    //         };
    //
    //         var results = await _emails.Aggregate<EmailDocument>(pipeline).ToListAsync();
    //         _logger.LogInformation("Found {Count} similar emails", results.Count);
    //         return results;
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogError(ex, "Failed to find similar emails");
    //         throw;
    //     }
    // }

    public async Task<List<EmailDocument>> FindSimilarEmailsAsync(
        List<float> queryEmbedding,
        string provider,
        DateTime? startDate = null,
        DateTime? endDate = null,
        int limit = 5)
    {
        try
        {
            // Najpierw sprawdźmy czy w ogóle mamy maile z embeddingami
            var testFilter = Builders<EmailDocument>.Filter.And(
                Builders<EmailDocument>.Filter.Eq(e => e.Provider, provider),
                Builders<EmailDocument>.Filter.Ne(e => e.Embedding, null),
                Builders<EmailDocument>.Filter.SizeGt(e => e.Embedding, 0)
            );

            var testCount = await _emails.CountDocumentsAsync(testFilter);
            _logger.LogInformation("Found {Count} emails with embeddings for provider {Provider}", testCount, provider);

            if (testCount == 0)
            {
                _logger.LogWarning("No emails with embeddings found!");
                return new List<EmailDocument>();
            }
            
            // Better logging
            _logger.LogInformation(
                "Pipeline details - QueryEmbedding length: {Length}, First 3 values: [{Values}]", 
                queryEmbedding.Count,
                string.Join(", ", queryEmbedding.Take(3)));

            var sampleDoc = await _emails.Find(testFilter).Limit(1).FirstOrDefaultAsync();
            if (sampleDoc != null)
            {
                _logger.LogInformation(
                    "Sample document - Embedding length: {Length}, First 3 values: [{Values}]",
                    sampleDoc.Embedding?.Count ?? 0,
                    sampleDoc.Embedding != null ? string.Join(", ", sampleDoc.Embedding.Take(3)) : "null");
            }

            var filterBuilder = Builders<EmailDocument>.Filter;
            var filters = new List<FilterDefinition<EmailDocument>>
            {
                filterBuilder.Eq(e => e.Provider, provider),
                filterBuilder.Ne(e => e.Embedding, null), // Embedding nie może być null
                filterBuilder.SizeGt(e => e.Embedding, 0) // Embedding musi mieć elementy
            };

            if (startDate.HasValue)
                filters.Add(filterBuilder.Gte(e => e.ReceivedDate, startDate.Value));
            if (endDate.HasValue)
                filters.Add(filterBuilder.Lte(e => e.ReceivedDate, endDate.Value));

            var filter = filterBuilder.And(filters);

            _logger.LogInformation("Executing similarity search pipeline");

            var queryEmbeddingArray = new BsonArray(queryEmbedding.Select(x => (double)x));

            var pipeline = new[]
            {
                new BsonDocument("$match", filter.Render(
                    _emails.DocumentSerializer,
                    _emails.Settings.SerializerRegistry)),

                new BsonDocument("$addFields", new BsonDocument
                {
                    {
                        "similarity", new BsonDocument("$ifNull", new BsonArray
                        {
                            new BsonDocument("$reduce", new BsonDocument
                            {
                                { "input", new BsonDocument("$range", new BsonArray { 0, queryEmbedding.Count }) },
                                { "initialValue", 0.0 },
                                {
                                    "in", new BsonDocument("$add", new BsonArray
                                    {
                                        "$$value",
                                        new BsonDocument("$multiply", new BsonArray
                                        {
                                            new BsonDocument("$convert", new BsonDocument
                                            {
                                                { "input", new BsonDocument("$arrayElemAt", new BsonArray { "$embedding", "$$this" }) },
                                                { "to", "double" }
                                            }),
                                            new BsonDocument("$arrayElemAt", new BsonArray 
                                            { 
                                                queryEmbeddingArray,
                                                "$$this" 
                                            })
                                        })
                                    })
                                }
                            }),
                            0.0
                        })
                    }
                }),
                new BsonDocument("$sort", new BsonDocument("similarity", -1)),
                new BsonDocument("$limit", limit)
            };

            var results = await _emails.Aggregate<EmailDocument>(pipeline).ToListAsync();
            _logger.LogInformation("Found {Count} similar emails", results.Count);

            foreach (var result in results)
            {
                _logger.LogInformation("Found similar email: {Subject} with embedding length: {Length}",
                    result.Subject, result.Embedding?.Count ?? 0);
            }

            return results;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to find similar emails");
            throw;
        }
    }
}