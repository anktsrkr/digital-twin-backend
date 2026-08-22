using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using ResumeAssistant.Core.Models;
using VoyageAI;

namespace ResumeAssistant.Api.Services;

/// <summary>
/// MongoDB vector searcher that implements <see cref="IVoyageRagSearcher{T}"/> for the Agentic RAG pipeline.
/// Embeds queries with Jina AI (1024 dimensions) and executes MongoDB Atlas Vector Search ($vectorSearch)
/// across the `resume_chunks` collection, with automatic in-memory vector ranking and keyword fallback.
/// </summary>
public sealed class MongoDbRagSearcher : IVoyageRagSearcher<ResumeChunk>
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IMongoDatabase? _database;
    private readonly ILogger<MongoDbRagSearcher> _logger;

    public MongoDbRagSearcher(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IMongoDatabase? database,
        ILogger<MongoDbRagSearcher> logger)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _database = database;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public const double MinRelevanceScoreThreshold = 0.65;

    private static double ComputeCosineSimilarity(float[] vecA, float[] vecB)
    {
        if (vecA.Length != vecB.Length || vecA.Length == 0) return 0;
        double dot = 0, magA = 0, magB = 0;
        for (int i = 0; i < vecA.Length; i++)
        {
            dot += vecA[i] * vecB[i];
            magA += vecA[i] * vecA[i];
            magB += vecB[i] * vecB[i];
        }
        if (magA == 0 || magB == 0) return 0;
        return dot / (Math.Sqrt(magA) * Math.Sqrt(magB));
    }

    public async Task<IReadOnlyList<VoyageSearchResult<ResumeChunk>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        if (_database is null)
        {
            _logger.LogWarning("MongoDB database instance is not configured. Returning empty search results.");
            return [];
        }

        var collection = _database.GetCollection<ResumeChunk>("resume_chunks");
        float[]? queryVector = null;

        try
        {
            _logger.LogInformation("Generating Jina AI query embedding for query: '{Query}'", query);

            // 1. Generate 1024-dim query vector using Jina AI (task: retrieval.query)
            var options = new EmbeddingGenerationOptions();
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["InputType"] = "Query";
            options.AdditionalProperties["Task"] = "retrieval.query";

            var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(
                [query],
                options,
                cancellationToken).ConfigureAwait(false);

            if (generatedEmbeddings.Count > 0)
            {
                queryVector = generatedEmbeddings[0].Vector.ToArray();
                var doubleVector = queryVector.Select(f => (double)f).ToArray();

                // 2. Attempt MongoDB Atlas $vectorSearch pipeline stage
                var pipeline = new BsonDocument[]
                {
                    new BsonDocument("$vectorSearch", new BsonDocument
                    {
                        { "index", "vector_index" },
                        { "path", "embedding" },
                        { "queryVector", new BsonArray(doubleVector) },
                        { "numCandidates", 50 },
                        { "limit", 15 }
                    }),
                    new BsonDocument("$project", new BsonDocument
                    {
                        { "_id", 1 },
                        { "title", 1 },
                        { "category", 1 },
                        { "company", 1 },
                        { "role", 1 },
                        { "start_date", 1 },
                        { "end_date", 1 },
                        { "content", 1 },
                        { "source_name", 1 },
                        { "source_link", 1 },
                        { "technologies", 1 },
                        { "score", new BsonDocument("$meta", "vectorSearchScore") }
                    })
                };

                var cursor = await collection.AggregateAsync<ResumeChunk>(pipeline, cancellationToken: cancellationToken).ConfigureAwait(false);
                var searchResults = await cursor.ToListAsync(cancellationToken).ConfigureAwait(false);

                if (searchResults.Count > 0)
                {
                    var relevantCandidates = searchResults
                        .Where(c => (c.Score ?? 0) >= MinRelevanceScoreThreshold)
                        .ToList();

                    if (relevantCandidates.Count > 0)
                    {
                        _logger.LogInformation("Retrieved {Count} relevant candidates (score >= {Threshold}) from MongoDB $vectorSearch for query '{Query}'.",
                            relevantCandidates.Count, MinRelevanceScoreThreshold, query);

                        return relevantCandidates
                            .Select(chunk => new VoyageSearchResult<ResumeChunk>
                            {
                                Record = chunk,
                                Text = chunk.ToContextString()
                            })
                            .ToList();
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "MongoDB $vectorSearch stage not available or indexing for '{Query}'.", query);
        }

        // 3. In-Memory Vector Search Fallback (computes exact cosine similarity across stored 1024-dim vectors)
        if (queryVector is not null)
        {
            try
            {
                var storedChunks = await collection.Find(Builders<ResumeChunk>.Filter.Ne(c => c.Embedding, null))
                    .ToListAsync(cancellationToken)
                    .ConfigureAwait(false);

                if (storedChunks.Count > 0)
                {
                    var rankedVectors = storedChunks
                        .Select(c =>
                        {
                            c.Score = c.Embedding is not null ? ComputeCosineSimilarity(queryVector, c.Embedding) : 0;
                            return c;
                        })
                        .Where(c => (c.Score ?? 0) >= MinRelevanceScoreThreshold)
                        .OrderByDescending(c => c.Score)
                        .Take(6)
                        .ToList();

                    if (rankedVectors.Count > 0)
                    {
                        _logger.LogInformation("Retrieved {Count} relevant candidates via high-precision vector cosine similarity (top score: {TopScore:F4}) for query '{Query}'.",
                            rankedVectors.Count, rankedVectors[0].Score, query);

                        return rankedVectors
                            .Select(chunk => new VoyageSearchResult<ResumeChunk>
                            {
                                Record = chunk,
                                Text = chunk.ToContextString()
                            })
                            .ToList();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "In-memory vector cosine evaluation failed for query '{Query}'.", query);
            }
        }

        // 4. Keyword Fallback as additional safety net
        try
        {
            _logger.LogInformation("Executing keyword search fallback for '{Query}'...", query);
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var filterBuilder = Builders<ResumeChunk>.Filter;
            var filters = new List<FilterDefinition<ResumeChunk>>();

            foreach (var term in terms)
            {
                if (term.Length < 3) continue;
                var regex = new BsonRegularExpression(term, "i");
                filters.Add(filterBuilder.Or(
                    filterBuilder.Regex(c => c.Title, regex),
                    filterBuilder.Regex(c => c.Content, regex),
                    filterBuilder.Regex(c => c.Category, regex),
                    filterBuilder.Regex(c => c.Company, regex)
                ));
            }

            var combinedFilter = filters.Count > 0 ? filterBuilder.Or(filters) : filterBuilder.Empty;
            var fallbackChunks = await collection.Find(combinedFilter).Limit(6).ToListAsync(cancellationToken).ConfigureAwait(false);

            if (fallbackChunks.Count > 0)
            {
                _logger.LogInformation("Retrieved {Count} candidates from MongoDB keyword fallback for query '{Query}'.", fallbackChunks.Count, query);

                return fallbackChunks
                    .Select(chunk => new VoyageSearchResult<ResumeChunk>
                    {
                        Record = chunk,
                        Text = chunk.ToContextString()
                    })
                    .ToList();
            }
        }
        catch (Exception innerEx)
        {
            _logger.LogError(innerEx, "MongoDB fallback search failed for query '{Query}'.", query);
        }

        return [];
    }
}
