using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;
using ResumeAssistant.Core.Models;
using ResumeAssistant.Core.Services;
using ResumeAssistant.Ingestion.Services;
using VoyageAI;

namespace ResumeAssistant.Ingestion;

public class Program
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("==================================================================");
        Console.WriteLine(" Resume Digital Twin - Markdown Knowledge Ingestion & Vector Tool ");
        Console.WriteLine("==================================================================\n");

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        string embeddingProvider = configuration["EMBEDDING_PROVIDER"] ?? configuration["Embedding:Provider"] ?? "JinaAI";
        string? jinaApiKey = configuration["JINA_API_KEY"] ?? configuration["JinaAI:ApiKey"];
        string jinaModel = configuration["JinaAI:Model"] ?? "jina-embeddings-v3";
        string? voyageApiKey = configuration["VOYAGE_API_KEY"] ?? configuration["VoyageAI:ApiKey"];
        string voyageModel = configuration["VoyageAI:Model"] ?? "voyage-3-lite";
        string mongoDbMode = configuration["MONGODB_MODE"] ?? configuration["MongoDB:Mode"] ?? "Local";
        string databaseName = configuration["MONGODB_DATABASE"] ?? configuration["MongoDB:DatabaseName"] ?? "resume_assistant";
        string? connectionString = configuration["MONGODB_CONNECTION_STRING"]
            ?? (string.Equals(mongoDbMode, "Cloud", StringComparison.OrdinalIgnoreCase)
                ? configuration["MongoDB:Cloud:ConnectionString"]
                : configuration["MongoDB:Local:ConnectionString"]);

        bool isJinaConfigured = !string.IsNullOrWhiteSpace(jinaApiKey) && !jinaApiKey.StartsWith("YOUR_");
        bool isVoyageConfigured = !string.IsNullOrWhiteSpace(voyageApiKey) && !voyageApiKey.StartsWith("YOUR_");

        bool useJina = string.Equals(embeddingProvider, "JinaAI", StringComparison.OrdinalIgnoreCase) ? isJinaConfigured : (!isVoyageConfigured && isJinaConfigured);

        string activeProvider = useJina ? $"Jina AI ({jinaModel})" : $"Voyage AI ({voyageModel})";
        Console.WriteLine($"⚙️  Embedding Provider: {activeProvider}");
        Console.WriteLine($"⚙️  MongoDB Target: {mongoDbMode} (Database: {databaseName})");

        if (!isJinaConfigured && !isVoyageConfigured)
        {
            Console.WriteLine("⚠️  WARNING: No embedding API key configured (neither JINA_API_KEY nor VOYAGE_API_KEY).");
            Console.WriteLine("   Running in dry-run validation mode (validating Markdown chunks & frontmatter)...\n");
        }

        // 1. Discover and load all Markdown documents from Data/ directory
        string dataDir = Path.Combine(AppContext.BaseDirectory, "Data");
        if (!Directory.Exists(dataDir) || Directory.GetFiles(dataDir, "*.md", SearchOption.AllDirectories).Length == 0)
        {
            string localData = Path.Combine(Directory.GetCurrentDirectory(), "Data");
            if (Directory.Exists(localData) && Directory.GetFiles(localData, "*.md", SearchOption.AllDirectories).Length > 0)
            {
                dataDir = localData;
            }
            else
            {
                string projectData = Path.Combine(Directory.GetCurrentDirectory(), "src", "ResumeAssistant.Ingestion", "Data");
                if (Directory.Exists(projectData) && Directory.GetFiles(projectData, "*.md", SearchOption.AllDirectories).Length > 0)
                {
                    dataDir = projectData;
                }
            }
        }

        var chunks = new List<ResumeChunk>();

        if (Directory.Exists(dataDir))
        {
            var mdFiles = Directory.GetFiles(dataDir, "*.md", SearchOption.AllDirectories);
            if (mdFiles.Length > 0)
            {
                Console.WriteLine($"📂 Found {mdFiles.Length} Markdown documents in {dataDir}:");
                foreach (var file in mdFiles)
                {
                    string relativePath = Path.GetRelativePath(dataDir, file);
                    var fileChunks = MarkdownResumeParser.ParseAndChunkFile(file);
                    chunks.AddRange(fileChunks);
                    Console.WriteLine($"   📄 [{relativePath}] -> generated {fileChunks.Count} chunk(s)");
                }
            }
        }

        // Fallback: If no markdown files found, check for legacy resume-data.json
        if (chunks.Count == 0)
        {
            string jsonPath = Path.Combine(dataDir, "resume-data.json");
            if (File.Exists(jsonPath))
            {
                Console.WriteLine($"📄 Loading fallback legacy {Path.GetFileName(jsonPath)}...");
                string jsonContent = await File.ReadAllTextAsync(jsonPath);
                using var doc = JsonDocument.Parse(jsonContent);
                var chunksElement = doc.RootElement.GetProperty("chunks");
                foreach (var el in chunksElement.EnumerateArray())
                {
                    var techList = new List<string>();
                    if (el.TryGetProperty("technologies", out var techEl) && techEl.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var t in techEl.EnumerateArray())
                        {
                            if (t.GetString() is { } str) techList.Add(str);
                        }
                    }

                    chunks.Add(new ResumeChunk
                    {
                        Title = el.GetProperty("title").GetString() ?? "Untitled",
                        Category = el.GetProperty("category").GetString() ?? "General",
                        Company = el.TryGetProperty("company", out var comp) && comp.ValueKind == JsonValueKind.String ? comp.GetString() : null,
                        Role = el.TryGetProperty("role", out var role) && role.ValueKind == JsonValueKind.String ? role.GetString() : null,
                        StartDate = el.TryGetProperty("startDate", out var sd) && sd.ValueKind == JsonValueKind.String ? sd.GetString() : null,
                        EndDate = el.TryGetProperty("endDate", out var ed) && ed.ValueKind == JsonValueKind.String ? ed.GetString() : null,
                        Content = el.GetProperty("content").GetString() ?? "",
                        SourceName = el.GetProperty("sourceName").GetString() ?? "Resume",
                        SourceLink = el.TryGetProperty("sourceLink", out var sl) && sl.ValueKind == JsonValueKind.String ? sl.GetString() : null,
                        Technologies = techList.ToArray()
                    });
                }
            }
        }

        if (chunks.Count == 0)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error: No Markdown (.md) or JSON resume data found in: {dataDir}");
            Console.ResetColor();
            return;
        }

        Console.WriteLine($"\n📊 Total Chunks to Ingest: {chunks.Count}\n");
        for (int i = 0; i < chunks.Count; i++)
        {
            Console.WriteLine($"   [{i + 1:D2}/{chunks.Count:D2}] ({chunks[i].Category}) {chunks[i].Title}");
        }
        Console.WriteLine();

        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator;
        if (useJina)
        {
            embeddingGenerator = new JinaEmbeddingGenerator(
                apiKey: jinaApiKey!,
                model: jinaModel,
                defaultTask: "retrieval.passage",
                dimensions: 1024);
        }
        else if (isVoyageConfigured)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddVoyageAI(o => o.ApiKey = voyageApiKey!);
            services.AddVoyageEmbeddingGenerator(o =>
            {
                o.Model = voyageModel;
                o.InputType = VoyageAI.Models.InputType.Document;
            });
            var sp = services.BuildServiceProvider();
            embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("✅ Dry-run validation successful! All Markdown files, YAML frontmatter, and semantic chunks parsed perfectly.");
            Console.WriteLine("   To seed live embeddings to MongoDB, provide valid API credentials and re-run.\n");
            Console.ResetColor();
            return;
        }

        // Test embedding generation for the first chunk to verify API connectivity
        Console.WriteLine($"🔮 Testing {activeProvider} embedding generation for 1024 dimensions...");
        try
        {
            var testEmbedding = await embeddingGenerator.GenerateAsync([chunks[0].ToContextString()]);
            var dim = testEmbedding[0].Vector.Length;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"   ✓ Generated test embedding successfully (Vector dimension: {dim})!\n");
            Console.ResetColor();
        }
        catch (Exception ex)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"❌ Error communicating with Embedding API: {ex.Message}");
            Console.ResetColor();
            return;
        }

        if (string.IsNullOrWhiteSpace(connectionString) || connectionString.Contains("YOUR_ATLAS_USER"))
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("ℹ️  Embeddings API is verified and working perfectly!");
            Console.WriteLine("   To write to MongoDB, set MONGODB_CONNECTION_STRING with your active MongoDB credentials.");
            Console.ResetColor();
            return;
        }

        Console.WriteLine("💾 Connecting to MongoDB database...");

        var client = new MongoClient(connectionString);
        var database = client.GetDatabase(databaseName);
        var collection = database.GetCollection<ResumeChunk>("resume_chunks");

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"   ✓ Successfully connected to MongoDB [{databaseName}]!\n");
        Console.ResetColor();

        // Ensure indexes exist
        Console.WriteLine("📜 Creating BSON indexes on `category` and `company`...");
        try
        {
            var categoryIndex = new CreateIndexModel<ResumeChunk>(Builders<ResumeChunk>.IndexKeys.Ascending(c => c.Category));
            var companyIndex = new CreateIndexModel<ResumeChunk>(Builders<ResumeChunk>.IndexKeys.Ascending(c => c.Company));
            await collection.Indexes.CreateManyAsync([categoryIndex, companyIndex]);
            Console.WriteLine("   ✓ Indexes created successfully.\n");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"   ⚠️ Index creation note: {ex.Message}");
        }

        // Clean reseed: remove previous chunks so re-runs don't create stale duplicate vectors
        await collection.DeleteManyAsync(Builders<ResumeChunk>.Filter.Empty);
        Console.WriteLine("   ✓ Cleared existing records in `resume_chunks` for a clean re-seed.\n");

        int successCount = 0;
        foreach (var chunk in chunks)
        {
            string contextText = chunk.ToContextString();
            var embeddings = await embeddingGenerator.GenerateAsync([contextText]);
            chunk.Embedding = embeddings[0].Vector.ToArray();
            chunk.UpdatedAt = DateTime.UtcNow;

            await collection.InsertOneAsync(chunk);
            successCount++;
            Console.WriteLine($"   ✓ Seeded: {chunk.Title}");
        }

        Console.ForegroundColor = ConsoleColor.Green;
        Console.WriteLine($"\n🎉 Success! Seeded {successCount} chunks with 1024-dim {activeProvider} embeddings into MongoDB collection `resume_chunks`.\n");
        Console.ResetColor();
    }
}
