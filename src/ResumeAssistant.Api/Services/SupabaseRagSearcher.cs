using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Npgsql;
using Pgvector;
using ResumeAssistant.Core.Models;
using VoyageAI;

namespace ResumeAssistant.Api.Services;

/// <summary>
/// Supabase PostgreSQL pgvector searcher that implements <see cref="IVoyageRagSearcher{T}"/> for the Voyage RAG pipeline.
/// Queries Supabase via the `match_resume_chunks` vector RPC and maps candidate records for Voyage reranking.
/// </summary>
public sealed class SupabaseRagSearcher : IVoyageRagSearcher<ResumeChunk>
{
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly NpgsqlDataSource? _dataSource;
    private readonly ILogger<SupabaseRagSearcher> _logger;
    private readonly List<ResumeChunk> _fallbackChunks = [];

    public SupabaseRagSearcher(
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        NpgsqlDataSource? dataSource,
        ILogger<SupabaseRagSearcher> logger)
    {
        _embeddingGenerator = embeddingGenerator ?? throw new ArgumentNullException(nameof(embeddingGenerator));
        _dataSource = dataSource;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // In-memory fallback chunks in case database is offline or running locally without Supabase DB
        InitializeFallbackChunks();
    }

    public async Task<IReadOnlyList<VoyageSearchResult<ResumeChunk>>> SearchAsync(
        string query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        _logger.LogInformation("Generating query embedding for query: '{Query}'", query);

        try
        {
            // 1. Embed the search query (supports Jina AI retrieval.query or Voyage AI query)
            var options = new EmbeddingGenerationOptions();
            options.AdditionalProperties ??= new AdditionalPropertiesDictionary();
            options.AdditionalProperties["InputType"] = "Query";

            var generatedEmbeddings = await _embeddingGenerator.GenerateAsync(
                [query],
                options,
                cancellationToken).ConfigureAwait(false);

            var queryVector = generatedEmbeddings[0].Vector;

            // 2. If PostgreSQL data source is available, query Supabase pgvector RPC
            if (_dataSource is not null)
            {
                var searchResults = new List<VoyageSearchResult<ResumeChunk>>();
                await using var conn = await _dataSource.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

                const string rpcSql = @"
                    SELECT id, title, category, company, role, start_date, end_date, content, source_name, source_link, technologies, similarity
                    FROM public.match_resume_chunks(@query_embedding, @match_count);";

                await using var cmd = new NpgsqlCommand(rpcSql, conn);
                cmd.Parameters.AddWithValue("query_embedding", new Vector(queryVector.ToArray()));
                cmd.Parameters.AddWithValue("match_count", 15);

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var chunk = new ResumeChunk
                    {
                        Id = reader.GetGuid(0),
                        Title = reader.GetString(1),
                        Category = reader.GetString(2),
                        Company = reader.IsDBNull(3) ? null : reader.GetString(3),
                        Role = reader.IsDBNull(4) ? null : reader.GetString(4),
                        StartDate = reader.IsDBNull(5) ? null : reader.GetString(5),
                        EndDate = reader.IsDBNull(6) ? null : reader.GetString(6),
                        Content = reader.GetString(7),
                        SourceName = reader.GetString(8),
                        SourceLink = reader.IsDBNull(9) ? null : reader.GetString(9),
                        Technologies = reader.IsDBNull(10) ? [] : (string[])reader.GetValue(10),
                        Similarity = reader.IsDBNull(11) ? null : reader.GetDouble(11)
                    };

                    searchResults.Add(new VoyageSearchResult<ResumeChunk>
                    {
                        Record = chunk,
                        Text = chunk.ToContextString()
                    });
                }

                if (searchResults.Count > 0)
                {
                    _logger.LogInformation("Retrieved {Count} candidates from Supabase pgvector.", searchResults.Count);
                    return searchResults;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase pgvector query failed or not configured. Falling back to in-memory semantic candidate pool.");
        }

        // 3. In-memory candidate pool fallback (for local development or dry-runs)
        _logger.LogInformation("Using in-memory candidate pool with {Count} chunks.", _fallbackChunks.Count);
        return _fallbackChunks
            .Select(chunk => new VoyageSearchResult<ResumeChunk>
            {
                Record = chunk,
                Text = chunk.ToContextString()
            })
            .ToList();
    }

    private void InitializeFallbackChunks()
    {
        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "Executive Summary & Professional Profile",
            Category = "About",
            Company = "Tata Consultancy Services",
            Role = "Principal Engineer | Azure Solutions Architect Expert",
            StartDate = "2013-09",
            EndDate = "Present",
            SourceName = "Resume: Professional Summary",
            SourceLink = "#overview",
            Technologies = ["Microsoft Azure", "Agentic AI", "Platform Engineering", "DevEx", "Distributed Systems", "C#", ".NET 10", "Terraform"],
            Content = "Principal Engineer and Microsoft Certified Azure Solutions Architect Expert with 13+ years of experience designing and delivering enterprise platforms, distributed systems, cloud modernisation programmes, and integration solutions across retail, logistics, and transportation environments in the UK, Belgium, and India. Specialises in platform engineering, distributed event-driven architecture, and Agentic AI."
        });

        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "Career Preferences, Target Roles & Work Authorisation",
            Category = "About",
            Role = "AI Solutions Architect | Technical Architect | Principal Engineer",
            SourceName = "Career Preferences & Work Authorisation",
            SourceLink = "#career-preferences",
            Technologies = ["AI Solutions Architect", "Technical Architect", "Principal Engineer", "UK Visa Sponsorship", "Remote Work", "Hybrid Work", "Leeds, UK"],
            Content = "Career Objectives & Preferences: Target Roles: AI Solutions Architect, Technical Architect, Principal Engineer, Enterprise Cloud Architect. Work Authorisation: Requires UK Skilled Worker Visa Sponsorship. Work Arrangement: Open to both Remote and Hybrid working arrangements (based in Leeds, UK)."
        });

        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "ASDA eCommerce Picking Platform: Technical Ownership & High Scale",
            Category = "Experience",
            Company = "ASDA / Major UK Grocery Retailer",
            Role = "Principal Engineer | Azure Solutions Architect | Technical Owner",
            StartDate = "2023-01",
            EndDate = "Present",
            SourceName = "Work Experience: ASDA eCommerce Platform",
            SourceLink = "#experience-asda",
            Technologies = ["Microsoft Azure", "C#", ".NET", "Event-Driven Architecture", "Azure Integration Services", "Service Bus", "Event Hubs", "Functions", "Terraform"],
            Content = "Technical Owner for ASDA's Azure-based eCommerce Picking Platform supporting 700,000+ weekly customer orders across 625 stores. Proven peak resilience handling 90,000+ orders in 30 minutes and 150,000+ Christmas orders with ZERO critical production incidents."
        });

        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "Agentic AI, Azure AI Foundry & Model Context Protocol (MCP)",
            Category = "Experience",
            Company = "ASDA / Enterprise AI Solutions",
            Role = "AI Solutions Architect",
            StartDate = "2023-01",
            EndDate = "Present",
            SourceName = "Work Experience: Agentic AI & AI Foundry",
            SourceLink = "#experience-agentic-ai",
            Technologies = ["Microsoft Agent Framework", "Azure AI Foundry", "MCP", "Agent2Agent (A2A)", "Azure OpenAI", "Anthropic Claude"],
            Content = "Architected secure Agentic AI solutions and multi-agent systems using Microsoft Agent Framework, Azure AI Foundry, Model Context Protocol (MCP), and Agent2Agent (A2A). Built custom GitHub Copilot Agents and reusable Agent Skills supporting modernization across 200+ repositories."
        });

        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "Enterprise RAG, Vector Search & Fine-Grained Authorization with SpiceDB",
            Category = "Experience",
            Company = "Enterprise AI Architecture",
            Role = "AI Solutions Architect",
            StartDate = "2023-01",
            EndDate = "Present",
            SourceName = "Architecture: Enterprise RAG & SpiceDB",
            SourceLink = "#architecture-rag-spicedb",
            Technologies = ["Enterprise RAG", "Vector Databases", "pgvector", "Voyage AI", "SpiceDB", "ReBAC", "Zero Trust AI"],
            Content = "Designed enterprise RAG platforms combining event-driven knowledge ingestion, vector similarity search, reranking, and fine-grained authorization using SpiceDB (ReBAC) to enforce organizational permissions before context injection."
        });

        _fallbackChunks.Add(new ResumeChunk
        {
            Title = "Boots UK (Walgreens Boots Alliance): Cloud Modernisation Programme",
            Category = "Experience",
            Company = "Boots UK / Walgreens Boots Alliance",
            Role = "Azure Solution Architect",
            StartDate = "2021-01",
            EndDate = "2022-12",
            SourceName = "Work Experience: Boots UK Cloud Modernisation",
            SourceLink = "#experience-boots",
            Technologies = ["Microsoft Azure", "Azure App Services", "APIM", "Stub Identity Platform", "Azure DevOps"],
            Content = "Led Azure architecture for 7 business-critical applications (25,000+ users). Designed a reusable Stub Identity Platform that unblocked enterprise performance testing. Supported cloud migration strategy across ~90 enterprise applications."
        });
    }
}
